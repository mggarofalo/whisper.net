// The dictation orchestrator: the coordination hub that runs one utterance end to end (WHISPER-14).
// A hotkey start request begins microphone capture through the IAudioSource port; a stop request
// finalizes the captured audio into a clip and drives it through the Application delivery pipeline
// (DeliverTranscriptionCommand via Mediator) — trim, transcribe, post-process, inject — with no manual
// step in between. It owns an explicit pipeline state machine (Idle -> Recording -> Transcribing ->
// Delivering -> Idle) guarded against concurrent transitions, and keeps the shared RecordingStateMachine
// in step so the tray/UI reflect status. Every cross-layer touch is an Application port (no Infrastructure
// type is referenced here), so the whole flow is unit-testable with faked ports. Any stage error is
// logged via Serilog and returns the pipeline to a safe Idle — no transition can leave it stuck.

using System.Diagnostics;
using Application.Ports;
using Application.Transcription;
using Domain.Audio;
using Logic.AudioManagement;
using Mediator;
using Microsoft.Extensions.Logging;

namespace Logic.AppManagement;

public sealed class DictationOrchestrator
{
	private readonly IAudioSource _audioSource;
	private readonly RecordingStateMachine _stateMachine;
	private readonly CaptureBuffer _captureBuffer;
	private readonly IMediator _mediator;
	private readonly ILogger<DictationOrchestrator> _logger;

	// Serializes stage reads/writes so overlapping signals (key auto-repeat, a stop racing a capture
	// failure) resolve to one accepted transition; the awaited delivery runs outside the lock.
	private readonly object _gate = new();

	public DictationOrchestrator(
		IAudioSource audioSource,
		RecordingStateMachine stateMachine,
		HotkeyActivationController activation,
		AudioResampler resampler,
		AudioBufferingOptions bufferingOptions,
		IMediator mediator,
		ILogger<DictationOrchestrator> logger)
	{
		_audioSource = audioSource;
		_stateMachine = stateMachine;
		_captureBuffer = new CaptureBuffer(bufferingOptions, resampler);
		_mediator = mediator;
		_logger = logger;

		_audioSource.FrameAvailable += OnFrameAvailable;
		_audioSource.CaptureFailed += OnCaptureFailed;

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

		_stateMachine.RequestStart();
		_captureBuffer.StartRecording();
		_audioSource.Start();
		_logger.LogInformation("Dictation recording started.");
	}

	/// <summary>
	/// Stop signal (release / VAD silence): finalize the capture and run the full delivery pipeline —
	/// Recording -> Transcribing -> Delivering -> Idle. A failure at any stage is logged and the pipeline
	/// is returned to a safe Idle so it can never get stuck.
	/// </summary>
	public async Task StopAsync(CancellationToken cancellationToken = default)
	{
		if (!TryAdvance(DictationStage.Recording, DictationStage.Transcribing))
		{
			return;
		}

		_audioSource.Stop();
		AudioClip clip = _captureBuffer.StopRecording();
		_stateMachine.RequestStop();

		long startedTicks = Stopwatch.GetTimestamp();
		try
		{
			// The Application delivery command fuses transcription and injection behind one Mediator call
			// (trim -> transcribe -> post-process -> UIPI check -> inject). The orchestrator awaits its
			// result, then marks the Delivering hand-off, so the explicit stage path and its durations are
			// observable without forking the proven delivery handler.
			DeliveryResult result = await _mediator.Send(new DeliverTranscriptionCommand(clip), cancellationToken);
			Advance(DictationStage.Delivering);

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
		}
		catch (Exception ex)
		{
			_logger.LogError(
				ex,
				"Dictation pipeline failed after {ElapsedMs:F1}ms; returning to Idle.",
				Stopwatch.GetElapsedTime(startedTicks).TotalMilliseconds);
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
		_captureBuffer.StopRecording(); // finalize-and-drop: the captured clip is discarded, never delivered.
		_stateMachine.Cancel();
		_logger.LogInformation("Dictation cancelled; capture discarded.");
	}

	// Accumulate each captured frame into the buffer while recording. Frames arrive on the capture thread
	// between Start and Stop, so the buffer is only ever appended to within one recording's lifetime.
	private void OnFrameAvailable(object? sender, AudioFrameAvailableEventArgs e) =>
		_captureBuffer.Append(e.Samples.Span, e.Format);

	// A device error mid-capture is a stage error (AC4): discard the partial capture, log it, and return
	// the pipeline to a safe Idle rather than leaving it stuck in Recording.
	private void OnCaptureFailed(object? sender, AudioCaptureFailedEventArgs e)
	{
		if (!TryAdvance(DictationStage.Recording, DictationStage.Idle))
		{
			return;
		}

		_captureBuffer.StopRecording(); // discard the partial capture
		_stateMachine.Cancel();
		_logger.LogError("Audio capture failed ({Error}): {Message}; returning to Idle.", e.Error, e.Message);
	}

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
