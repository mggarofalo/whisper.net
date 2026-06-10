// The dictation orchestrator: the coordination hub that runs one utterance end to end (WHISPER-14).
// A hotkey start request begins microphone capture through the IAudioSource port; a stop request
// keeps the capture buffer open through a short post-release grace window (WHISPER-112) — the device's
// stop is asynchronous, so the frames already in flight (the user's final syllables) arrive AFTER the
// stop request — then finalizes the captured audio into a clip and drives it through the Application
// delivery pipeline (DeliverTranscriptionCommand via Mediator) — trim, transcribe, post-process,
// inject — with no manual step in between, then records a delivered transcription to history
// (RecordTranscriptionCommand, WHISPER-110) so the History section and usage stats reflect real
// usage. It owns an explicit pipeline
// state machine (Idle -> Recording -> Transcribing -> Delivering -> Idle) guarded against concurrent
// transitions, and keeps the shared RecordingStateMachine in step so the tray/UI reflect status. The
// capture buffer's max duration is a SOFT limit (WHISPER-111): when a recording approaches and then
// reaches it, the orchestrator publishes DictationNearLimitMessage / DictationAtLimitMessage on the
// shared IMessenger so the UI can warn the user — recording continues and nothing is dropped. A HARD
// failsafe ceiling backs the soft limit: at HardLimitReached the orchestrator stops the dictation
// itself through the normal stop path — stop and transcribe, never discard — and publishes
// DictationHardLimitStopMessage so the UI can say why dictation stopped on its own. Every cross-layer
// touch is an Application port (no Infrastructure type is referenced here), so the whole flow is
// unit-testable with faked ports. Any stage error is logged via Serilog and returns the pipeline to a
// safe Idle — no transition can leave it stuck.

using System.Diagnostics;
using Application.Configuration;
using Application.Dictation;
using Application.History;
using Application.Ports;
using Application.Transcription;
using CommunityToolkit.Mvvm.Messaging;
using Domain.Audio;
using Domain.Feedback;
using Logic.AudioManagement;
using Mediator;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Logic.AppManagement;

public sealed class DictationOrchestrator
{
	private readonly IAudioSource _audioSource;
	private readonly RecordingStateMachine _stateMachine;
	private readonly CaptureBuffer _captureBuffer;
	private readonly AudioBufferingOptions _bufferingOptions;
	private readonly IMediator _mediator;
	private readonly IMessenger _messenger;
	private readonly IAudioFeedback _audioFeedback;
	private readonly IOptions<AudioFeedbackOptions> _feedbackOptions;
	private readonly IUserNotifier _userNotifier;
	private readonly TimeProvider _timeProvider;
	private readonly ILogger<DictationOrchestrator> _logger;

	// Serializes stage reads/writes so overlapping signals (key auto-repeat, a stop racing a capture
	// failure) resolve to one accepted transition; the awaited delivery runs outside the lock.
	private readonly object _gate = new();

	// Set by OnCaptureFailed (on the capture thread) when the device fails while a stop is draining the
	// post-release grace window (WHISPER-112), and read by the post-grace path before finalizing the
	// capture. volatile provides the cross-thread visibility; the flag is scoped to one in-flight stop
	// and reset on Start so a stale signal can never discard the next utterance.
	private volatile bool _captureFailedDuringStop;

	public DictationOrchestrator(
		IAudioSource audioSource,
		RecordingStateMachine stateMachine,
		HotkeyActivationController activation,
		AudioResampler resampler,
		AudioBufferingOptions bufferingOptions,
		IMediator mediator,
		IMessenger messenger,
		IAudioFeedback audioFeedback,
		IOptions<AudioFeedbackOptions> feedbackOptions,
		IUserNotifier userNotifier,
		TimeProvider timeProvider,
		ILogger<DictationOrchestrator> logger)
	{
		_audioSource = audioSource;
		_stateMachine = stateMachine;
		_captureBuffer = new CaptureBuffer(bufferingOptions, resampler);
		_bufferingOptions = bufferingOptions;
		_mediator = mediator;
		_messenger = messenger;
		_audioFeedback = audioFeedback;
		_feedbackOptions = feedbackOptions;
		_userNotifier = userNotifier;
		_timeProvider = timeProvider;
		_logger = logger;

		_audioSource.FrameAvailable += OnFrameAvailable;
		_audioSource.CaptureFailed += OnCaptureFailed;

		// Soft-limit signals (WHISPER-111): the capture buffer fires these once per recording, on the
		// capture thread, at 80% and 100% of the soft max duration. Recording continues either way —
		// the handlers stay tiny and only publish the strongly-typed message on the (thread-safe)
		// messenger so the UI can warn the user; they must never touch the pipeline state.
		_captureBuffer.NearMaxDuration += (_, _) => _messenger.Send(
			new DictationNearLimitMessage(_captureBuffer.RecordedDurationMs, _bufferingOptions.MaxDurationMs));
		_captureBuffer.MaxDurationReached += (_, _) => _messenger.Send(
			new DictationAtLimitMessage(_captureBuffer.RecordedDurationMs, _bufferingOptions.MaxDurationMs));

		// Hard failsafe (WHISPER-111): the soft-limit messages above have no consuming UI yet, so the
		// hard ceiling is the enforcement bound that keeps a runaway recording from growing without end.
		// It must take the NORMAL stop path — stop and transcribe, never discard. The event fires on the
		// capture thread, so the stop is dispatched exactly like the hotkey release below: a guarded
		// fire-and-forget StopAsync, whose entry transition (Recording -> Transcribing) makes any
		// duplicate or racing call a harmless no-op. The message is published first so the eventual UI
		// can tell the user why dictation stopped on its own.
		_captureBuffer.HardLimitReached += (_, _) =>
		{
			_messenger.Send(new DictationHardLimitStopMessage(_captureBuffer.RecordedDurationMs, _bufferingOptions.HardMaxDurationMs));
			_logger.LogWarning(
				"Recording reached the hard duration ceiling ({HardMaxDurationMs} ms); stopping and transcribing.",
				_bufferingOptions.HardMaxDurationMs);
			_ = StopAsync();
		};

		// The hotkey is the production start/stop signal (AC2): push-to-talk/toggle matching lives in the
		// controller, and the orchestrator only reacts to its decisions. The stop path is fire-and-forget
		// because it is async; StopAsync owns its error handling, so a faulted task never escapes unobserved.
		activation.RecordingStartRequested += (_, _) => Start();
		activation.RecordingStopRequested += (_, _) => _ = StopAsync();
	}

	/// <summary>The current pipeline stage. Idle at rest.</summary>
	public DictationStage Stage { get; private set; } = DictationStage.Idle;

	/// <summary>Raised on every accepted stage transition, carrying the previous and current stage.</summary>
	public event EventHandler<DictationStageChangedEventArgs>? StageChanged;

	/// <summary>
	/// Whether continuous dictation mode is active (WHISPER-28). While active, each completed utterance
	/// auto-restarts recording instead of returning to rest; Esc (<see cref="ExitContinuousMode"/>) turns
	/// it off. When inactive the pipeline is single-shot: one capture -> deliver -> idle.
	/// </summary>
	public bool ContinuousMode { get; private set; }

	/// <summary>
	/// Enter continuous dictation mode: after each delivery the orchestrator restarts recording for the
	/// next utterance until the user exits. Idempotent — entering while already active is a no-op.
	/// </summary>
	public void EnableContinuousMode()
	{
		if (ContinuousMode)
		{
			return;
		}

		ContinuousMode = true;
		_logger.LogInformation("Continuous dictation mode entered.");
	}

	/// <summary>
	/// Esc: exit continuous dictation mode and return the pipeline to Idle without auto-restarting. Any
	/// in-flight capture is discarded; an utterance already transcribing/delivering completes (it just
	/// won't restart). A no-op when continuous mode is already off, beyond discarding an active capture.
	/// </summary>
	public void ExitContinuousMode()
	{
		if (ContinuousMode)
		{
			ContinuousMode = false;
			_logger.LogInformation("Continuous dictation mode exited.");
		}

		// Discard an in-flight capture so the pipeline returns to Idle; a no-op if not currently recording.
		Cancel();
	}

	/// <summary>
	/// Start signal (hotkey press): Idle -> Recording, beginning capture through the audio port. Ignored
	/// unless currently Idle, so a repeated start (e.g. key auto-repeat) can never open a second capture.
	/// </summary>
	public void Start()
	{
		if (!TryAdvance(DictationStage.Idle, DictationStage.Recording))
		{
			return;
		}

		_captureFailedDuringStop = false; // a stale mid-grace failure signal must not discard this capture
		_stateMachine.RequestStart();
		_captureBuffer.StartRecording();
		_audioSource.Start();
		_logger.LogInformation("Dictation recording started.");
		PlayFeedback(FeedbackSound.RecordingStarted);
	}

	/// <summary>
	/// Stop signal (release / VAD silence): drain the device's in-flight capture tail through a short
	/// post-release grace window (WHISPER-112), then finalize the capture and run the full delivery
	/// pipeline — Recording -> Transcribing -> Delivering -> Idle. A failure at any stage is logged and
	/// the pipeline is returned to a safe Idle so it can never get stuck.
	/// </summary>
	public async Task StopAsync(CancellationToken cancellationToken = default)
	{
		if (!TryAdvance(DictationStage.Recording, DictationStage.Transcribing))
		{
			return;
		}

		_audioSource.Stop();
		_stateMachine.RequestStop();
		PlayFeedback(FeedbackSound.RecordingStopped);

		// Post-release grace window (WHISPER-112): the device's stop is asynchronous — frames already in
		// flight (plus the user's final syllables) keep arriving for a short moment after the stop
		// request. The capture buffer stays recording through the window so that tail lands in the clip
		// instead of falling into the idle preroll ring; only then is the recording finalized.
		if (!await WaitForCaptureTailAsync(cancellationToken))
		{
			return;
		}

		AudioClip clip = _captureBuffer.StopRecording();

		// Measured where the capture is finalized: how long the recorded audio ran, the usage measure
		// (WHISPER-24) the history record carries once delivery succeeds.
		TimeSpan audioDuration = clip.SampleRate > 0
			? TimeSpan.FromSeconds((double)clip.Samples.Count / clip.SampleRate)
			: TimeSpan.Zero;

		long startedTicks = Stopwatch.GetTimestamp();
		try
		{
			// The Application delivery command fuses transcription and injection behind one Mediator call
			// (trim -> transcribe -> post-process -> UIPI check -> inject). The orchestrator awaits its
			// result, then marks the Delivering hand-off, so the explicit stage path and its durations are
			// observable without forking the proven delivery handler.
			DeliveryResult result = await _mediator.Send(new DeliverTranscriptionCommand(clip), cancellationToken);
			Advance(DictationStage.Delivering);
			PlayFeedback(FeedbackSound.TranscriptionComplete);

			// Command-mode hook (WHISPER-35): a matched transcript was routed to the command branch instead
			// of being typed. Execution is out of scope here; the orchestrator records the routing.
			if (result.MatchedCommand is { } command)
			{
				_logger.LogInformation("Dictation routed transcript to command branch: {Command}.", command);
			}

			_logger.LogInformation(
				"Dictation delivered={Delivered} block={Block} text-length={Length} in {ElapsedMs:F1}ms.",
				result.Delivered,
				result.Block,
				result.Text.Length,
				Stopwatch.GetElapsedTime(startedTicks).TotalMilliseconds);

			// History write-through (WHISPER-110): a delivered transcription is recorded so the History
			// section and usage stats reflect real usage. Recording observes delivery, it is never a
			// dependency of it — the method owns its errors, so a failed write can neither surface as a
			// delivery failure nor keep the pipeline from returning to Idle.
			if (result.Delivered)
			{
				await RecordToHistoryAsync(result.Text, audioDuration, cancellationToken);
			}
		}
		catch (Exception ex)
		{
			_logger.LogError(
				ex,
				"Dictation pipeline failed after {ElapsedMs:F1}ms; returning to Idle.",
				Stopwatch.GetElapsedTime(startedTicks).TotalMilliseconds);

			// Surface the failure to the user (WHISPER-95): in a windowless app a log-only failure reads
			// as "nothing was typed". The notifier never throws, so this cannot mask the recovery below.
			_userNotifier.NotifyError(
				"Dictation failed",
				"Your speech could not be transcribed or delivered. The app is still running — please try again.");
		}
		finally
		{
			_stateMachine.CompleteTranscription();
			Advance(DictationStage.Idle);
		}

		// Continuous dictation (WHISPER-28): keep the pipeline live across utterances. Once the cycle has
		// returned to Idle, if continuous mode is still active (Esc did not exit it during the utterance),
		// automatically begin the next recording instead of resting. Each restart needs a fresh stop signal
		// to advance, so the loop cannot spin — it waits in Recording until the next release / VAD silence.
		if (ContinuousMode)
		{
			_logger.LogInformation("Continuous dictation mode active; auto-restarting recording for the next utterance.");
			Start();
		}
	}

	// Wait the configured post-release grace window on the injected clock so the device's in-flight
	// capture tail drains into the buffer (WHISPER-112). Returns false when the stop must not proceed to
	// delivery — in every false path the capture is discarded here (DiscardRecording: no clip is ever
	// materialized for audio nobody will hear) and the pipeline returned to a safe Idle: the wait was
	// cancelled; the capture device failed mid-grace (OnCaptureFailed cannot claim the Recording stage
	// then, so it signals this in-flight stop through the failure flag and leaves the buffer discard to
	// it); or — defensively — the stage was reclaimed out of Transcribing (no current transition can do
	// that: Cancel and the Recording-stage failure path both require Recording, so the guard exists for
	// a future transition, and the discard is harmless then — the buffer discard is reset-safe and the
	// state-machine transition is guarded). The failure
	// flag and the stage guard are merged into ONE final gate with the flag read last, immediately
	// before control returns to the finalize path: a failure landing in the gap between the grace delay
	// completing and a separate, earlier flag check would otherwise notify the user the microphone
	// failed and then deliver the partial clip anyway.
	private async Task<bool> WaitForCaptureTailAsync(CancellationToken cancellationToken)
	{
		int graceMs = _bufferingOptions.PostReleaseGraceMs;
		if (graceMs > 0)
		{
			try
			{
				await Task.Delay(TimeSpan.FromMilliseconds(graceMs), _timeProvider, cancellationToken);
			}
			catch (OperationCanceledException)
			{
				_captureBuffer.DiscardRecording(); // a cancelled stop delivers nothing; no snapshot is materialized.
				_stateMachine.CompleteTranscription();
				Advance(DictationStage.Idle);
				_logger.LogInformation("Dictation stop cancelled during the post-release grace window; capture discarded.");
				return false;
			}
		}

		if (Stage != DictationStage.Transcribing || _captureFailedDuringStop)
		{
			_captureBuffer.DiscardRecording(); // a failed device or reclaimed stage delivers nothing; no snapshot is materialized.
			_stateMachine.CompleteTranscription();
			Advance(DictationStage.Idle);
			_logger.LogInformation("Capture failed or stage reclaimed during the post-release grace window; capture discarded.");
			return false;
		}

		return true;
	}

	/// <summary>
	/// Esc: discard an in-flight capture and return to Idle without transcribing or delivering. A cancel
	/// from any non-recording stage is a no-op (nothing in-flight to discard at the capture stage).
	/// </summary>
	public void Cancel()
	{
		if (!TryAdvance(DictationStage.Recording, DictationStage.Idle))
		{
			return;
		}

		_audioSource.Stop();
		_captureBuffer.DiscardRecording(); // the captured audio is discarded, never delivered — and never materialized.
		_stateMachine.Cancel();
		_logger.LogInformation("Dictation cancelled; capture discarded.");
	}

	// Record the delivered transcription in history through the Application command (append + retention
	// prune). Any failure is logged as a warning and swallowed: the text already reached the user, so a
	// history-write problem must never read as a dictation failure.
	private async Task RecordToHistoryAsync(string text, TimeSpan audioDuration, CancellationToken cancellationToken)
	{
		try
		{
			await _mediator.Send(new RecordTranscriptionCommand(text, DateTimeOffset.UtcNow, audioDuration), cancellationToken);
		}
		catch (Exception ex)
		{
			_logger.LogWarning(ex, "Recording the delivered transcription to history failed; the delivery itself succeeded.");
		}
	}

	// Audio feedback (WHISPER-21): play the cue for a pipeline transition, but only when feedback is
	// enabled (off => no cue and no playback resource is touched). Playback is fire-and-forget and must
	// never break dictation, so any failure is logged and swallowed here even though the port also
	// promises not to throw — feedback is a courtesy, never a dependency of the pipeline.
	private void PlayFeedback(FeedbackSound sound)
	{
		if (!_feedbackOptions.Value.Enabled)
		{
			return;
		}

		try
		{
			_audioFeedback.Play(sound);
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Audio feedback for {Sound} failed; ignored.", sound);
		}
	}

	// Accumulate each captured frame into the buffer while recording. Frames arrive on the capture thread
	// between Start and Stop, so the buffer is only ever appended to within one recording's lifetime.
	private void OnFrameAvailable(object? sender, AudioFrameAvailableEventArgs e) =>
		_captureBuffer.Append(e.Samples.Span, e.Format);

	// A device error mid-capture is a stage error (AC4): discard the partial capture, log it, and return
	// the pipeline to a safe Idle rather than leaving it stuck in Recording. A failure during the
	// post-release grace window (WHISPER-112) lands after the stop already advanced the pipeline to
	// Transcribing; it is logged and surfaced identically, but the buffer finalization stays owned by
	// the in-flight stop — the failure flag tells its post-grace check to discard instead of deliver.
	private void OnCaptureFailed(object? sender, AudioCaptureFailedEventArgs e)
	{
		if (TryAdvance(DictationStage.Recording, DictationStage.Idle))
		{
			_captureBuffer.DiscardRecording(); // discard the partial capture without materializing it
			_stateMachine.Cancel();
			_logger.LogError("Audio capture failed ({Error}): {Message}; returning to Idle.", e.Error, e.Message);
			NotifyCaptureFailure();
			return;
		}

		if (Stage != DictationStage.Transcribing)
		{
			return; // nothing in flight to claim (Delivering/Idle): the capture is already finalized or absent.
		}

		_captureFailedDuringStop = true;
		_logger.LogError(
			"Audio capture failed ({Error}): {Message} during the post-release grace window; the in-flight stop will discard the capture.",
			e.Error,
			e.Message);
		NotifyCaptureFailure();
	}

	// Surface a capture-device failure to the user (WHISPER-95): the recording is discarded either way,
	// and in a windowless app a log-only failure reads as "nothing was typed".
	private void NotifyCaptureFailure() =>
		_userNotifier.NotifyError(
			"Microphone problem",
			"Audio capture stopped unexpectedly, so the recording was discarded. Check your microphone and try again.");

	// Guarded conditional transition: advance only from the expected stage. Returns whether it moved, and
	// raises StageChanged outside the lock so a subscriber can never re-enter the gate.
	private bool TryAdvance(DictationStage expected, DictationStage next)
	{
		DictationStage previous;
		lock (_gate)
		{
			if (Stage != expected)
			{
				return false;
			}

			previous = Stage;
			Stage = next;
		}

		StageChanged?.Invoke(this, new DictationStageChangedEventArgs(previous, next));
		return true;
	}

	// Unconditional transition for the always-taken steps (the Delivering mark and the return to Idle in
	// the finally), kept idempotent so re-entry to the same stage raises nothing.
	private void Advance(DictationStage next)
	{
		DictationStage previous;
		lock (_gate)
		{
			previous = Stage;
			if (previous == next)
			{
				return;
			}

			Stage = next;
		}

		StageChanged?.Invoke(this, new DictationStageChangedEventArgs(previous, next));
	}
}
