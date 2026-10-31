// The level overlay's coordination logic, kept out of Presentation so it can be driven for real in
// specs. It mirrors the live recording state (subscribing to the RecordingStateMachine the orchestrator
// drives) into an IsVisible flag and a presentation State (Recording / Transcribing / Error), turns
// the audio frames captured during recording into a smoothed, perceptual input level in [0, 1] the
// meter reflects, tracks the elapsed recording time on the injected TimeProvider, and listens on the
// shared IMessenger for soft/hard limit signals (raising a near-cap warning before any audio could be
// lost) and for a dictation failure (a brief, auto-dismissed error state). The thin WPF overlay binds
// to this; it owns no UI itself, and the level math never touches or blocks the dictation pipeline (it
// only observes the frames the capture already raises).

using Application.Dictation;
using Application.Models;
using Application.Ports;
using CommunityToolkit.Mvvm.Messaging;
using Domain.Recording;
using Logic.AudioManagement;

namespace Logic.AppManagement;

public sealed class LevelOverlayController : IDisposable
{
	// Exponential smoothing: how much each new frame moves the displayed level. Low enough that the meter
	// does not jitter frame-to-frame (no jank), high enough that it still feels live.
	private const double SmoothingFactor = 0.3;

	// The dBFS window the meter spans. Raw RMS is near 0 for silence and ~1 only at full
	// digital scale, but speech sits around 0.02-0.1 RMS, so a linear meter barely moves. Mapping RMS to
	// dBFS and normalizing this window to [0, 1] puts normal speech (~-26 dBFS) mid-bar, floors anything
	// below -60 dBFS at silence, and lets loud speech approach full scale without pegging unless it is
	// actually at 0 dBFS.
	private const double MinimumDecibels = -60.0;

	// How long the error state lingers before the overlay dismisses itself: long enough to
	// read, short enough not to nag in a windowless app.
	private static readonly TimeSpan ErrorDismissAfter = TimeSpan.FromSeconds(4);

	// The elapsed-time tick cadence. Self-rescheduled one-shot (re-armed each tick) so it advances both
	// under a real TimeProvider and under the tests' one-shot manual clock.
	private static readonly TimeSpan ElapsedTick = TimeSpan.FromSeconds(1);

	// How long a recording may hear nothing before the pill asks whether the mic is muted (WHISPER-144):
	// long enough that a normal pause before speaking never triggers it.
	private static readonly TimeSpan SilenceHintAfter = TimeSpan.FromSeconds(3);

	/// <summary>Shown while recording once <see cref="SilenceHintAfter"/> has passed with no audio.</summary>
	public const string SilenceHintMessage = "No audio — is your mic muted?";

	/// <summary>Shown briefly after a recording ended with no audio at all.</summary>
	public const string NoAudioMessage = "No audio detected — is your mic muted?";

	private readonly RecordingStateMachine _stateMachine;
	private readonly IAudioSource _audioSource;
	private readonly IMessenger _messenger;
	private readonly TimeProvider _timeProvider;
	private readonly ITimer _elapsedTimer;
	private readonly ITimer _dismissTimer;
	private readonly ITimer _silenceTimer;

	// The RMS energy that counts as "the mic picked something up". Shared with the silence trimmer, so the
	// hint agrees with what the pipeline will treat as no audio: below it the clip is trimmed to nothing.
	private readonly double _speechEnergyThreshold;

	private DateTimeOffset _recordingStartedAt;

	// Whether the current recording has captured any input above the threshold. Written on the capture
	// thread and read on the silence timer's thread, hence volatile.
	private volatile bool _heardInput;

	// The model warm-up signal, tracked independently of the recording state: a background
	// reason to show the pill. Recording/transcribing/error always take precedence; warming only owns the
	// pill while the pipeline is otherwise at rest.
	private bool _isWarming;

	public LevelOverlayController(
		RecordingStateMachine stateMachine,
		IAudioSource audioSource,
		IMessenger messenger,
		TimeProvider timeProvider,
		SilenceTrimmerOptions? silenceTrimmerOptions = null)
	{
		_stateMachine = stateMachine;
		_audioSource = audioSource;
		_messenger = messenger;
		_timeProvider = timeProvider;
		_speechEnergyThreshold = (silenceTrimmerOptions ?? new SilenceTrimmerOptions()).EnergyThreshold;

		IsVisible = stateMachine.State == RecordingState.Recording;
		State = OverlayState.Recording;

		_elapsedTimer = _timeProvider.CreateTimer(_ => OnElapsedTick(), null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
		_dismissTimer = _timeProvider.CreateTimer(_ => OnErrorDismissed(), null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
		_silenceTimer = _timeProvider.CreateTimer(_ => OnSilenceTimeout(), null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);

		_stateMachine.StateChanged += OnStateChanged;
		_audioSource.FrameAvailable += OnFrameAvailable;

		// The soft/hard limit signals and the failure signal arrive on the shared messenger (published by
		// the orchestrator). Weak registration; removed on Dispose.
		_messenger.Register<LevelOverlayController, DictationNearLimitMessage>(this, (recipient, _) => recipient.RaiseNearCap());
		_messenger.Register<LevelOverlayController, DictationAtLimitMessage>(this, (recipient, _) => recipient.RaiseNearCap());
		_messenger.Register<LevelOverlayController, DictationHardLimitStopMessage>(this, (recipient, _) => recipient.RaiseNearCap());
		_messenger.Register<LevelOverlayController, DictationFailedMessage>(this, (recipient, _) => recipient.RaiseError());
		_messenger.Register<LevelOverlayController, DictationNoAudioMessage>(this, (recipient, _) => recipient.RaiseNoAudio());

		// Model warm-up: the warm-up service broadcasts started/cleared on the same messenger,
		// so the pill can announce "warming up" until the model is ready. Weak registration; removed on Dispose.
		_messenger.Register<LevelOverlayController, ModelWarmupChangedMessage>(this, (recipient, message) => recipient.OnWarmupChanged(message.IsWarming));
	}

	/// <summary>Whether the overlay should be shown — true while recording, transcribing, or briefly on error.</summary>
	public bool IsVisible { get; private set; }

	/// <summary>The smoothed microphone input level in [0, 1] the meter reflects.</summary>
	public double Level { get; private set; }

	/// <summary>The presentation state the overlay communicates: recording, transcribing, or error.</summary>
	public OverlayState State { get; private set; }

	/// <summary>Elapsed time of the current recording; resets to zero when a new recording starts.</summary>
	public TimeSpan Elapsed { get; private set; }

	/// <summary>True once the current recording has neared (or reached) the duration cap — a warning before
	/// any audio could be lost. Reset when the next recording starts.</summary>
	public bool NearCap { get; private set; }

	/// <summary>
	/// True while recording once <see cref="SilenceHintAfter"/> has passed without any input above the
	/// silence threshold; cleared the moment input crosses it. Reset when the next recording starts.
	/// </summary>
	public bool SilenceHint { get; private set; }

	/// <summary>
	/// The text line the pill shows under its meter, or null for none: the muted-mic hint while recording,
	/// or the no-audio message after a silent recording (WHISPER-144). Changes raise <see cref="StateChanged"/>.
	/// </summary>
	public string? Message =>
		State == OverlayState.NoAudio ? NoAudioMessage
		: State == OverlayState.Recording && SilenceHint ? SilenceHintMessage
		: null;

	/// <summary>Raised when <see cref="IsVisible"/> changes, so the view can show/hide the overlay.</summary>
	public event EventHandler? VisibilityChanged;

	/// <summary>Raised when <see cref="Level"/> changes, so the view can refresh the meter.</summary>
	public event EventHandler? LevelChanged;

	/// <summary>Raised when <see cref="State"/> or <see cref="NearCap"/> changes, so the view can restyle.</summary>
	public event EventHandler? StateChanged;

	/// <summary>Raised when <see cref="Elapsed"/> changes, so the view can refresh the timer text.</summary>
	public event EventHandler? ElapsedChanged;

	/// <summary>
	/// Raised every time a dictation takes over the pill (recording starts, or a failure is shown), whether
	/// or not the overlay was already visible. The window re-places itself, forces itself on top and verifies
	/// it is really on screen. A visibility change alone is not enough: a dictation started while the warming
	/// pill was up changed nothing visible, so the window never re-ran its show path (WHISPER-143).
	/// </summary>
	public event EventHandler? PresentRequested;

	private void OnStateChanged(object? sender, RecordingStateChangedEventArgs e)
	{
		switch (e.Current)
		{
			case RecordingState.Recording:
				StartRecordingFeedback();
				break;

			case RecordingState.Transcribing:
				EnterTranscribing();
				break;

			default:
				ReturnToRest();
				break;
		}
	}

	private void StartRecordingFeedback()
	{
		State = OverlayState.Recording;
		NearCap = false;
		Elapsed = TimeSpan.Zero;
		_recordingStartedAt = _timeProvider.GetUtcNow();
		_dismissTimer.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
		_elapsedTimer.Change(ElapsedTick, Timeout.InfiniteTimeSpan);

		// Arm the muted-mic hint: it fires only if nothing above the threshold arrives first.
		_heardInput = false;
		SilenceHint = false;
		_silenceTimer.Change(SilenceHintAfter, Timeout.InfiniteTimeSpan);

		SetVisible(true);
		StateChanged?.Invoke(this, EventArgs.Empty);
		ElapsedChanged?.Invoke(this, EventArgs.Empty);
		PresentRequested?.Invoke(this, EventArgs.Empty);
	}

	private void EnterTranscribing()
	{
		State = OverlayState.Transcribing;
		_elapsedTimer.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
		DisarmSilenceHint();

		// The meter is a recording cue; quiet it while transcribing so a stale level does not linger.
		ResetLevel();
		SetVisible(true);
		StateChanged?.Invoke(this, EventArgs.Empty);
	}

	private void ReturnToRest()
	{
		_elapsedTimer.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
		DisarmSilenceHint();

		// An error or no-audio message lingers (its own dismiss timer hides it); a normal return to Idle
		// hides immediately.
		if (State is OverlayState.Error or OverlayState.NoAudio)
		{
			return;
		}

		ResetLevel();
		Elapsed = TimeSpan.Zero;
		NearCap = false;

		// The recording is over; if the model is still warming, the pill stays up showing that instead of
		// vanishing — otherwise it hides. ShowWarmingOrHide owns the State + visibility for both outcomes.
		ShowWarmingOrHide();
	}

	private void OnFrameAvailable(object? sender, AudioFrameAvailableEventArgs e)
	{
		// Only meter while the overlay is showing; ignore the idle preroll frames the capture may raise.
		if (!IsVisible)
		{
			return;
		}

		double rms = RootMeanSquare(e.Samples.Span);
		double perceptual = ToPerceptualLevel(rms);
		Level = (SmoothingFactor * perceptual) + ((1 - SmoothingFactor) * Level);
		LevelChanged?.Invoke(this, EventArgs.Empty);

		// The mic picked something up: the hint never fires for this recording, and clears if it already did
		// (a quiet room, then the user starts talking).
		if (rms >= _speechEnergyThreshold && !_heardInput && State == OverlayState.Recording)
		{
			_heardInput = true;
			_silenceTimer.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
			if (SilenceHint)
			{
				SilenceHint = false;
				StateChanged?.Invoke(this, EventArgs.Empty);
			}
		}
	}

	private void OnSilenceTimeout()
	{
		// Re-checked here: speech may have landed on the capture thread just as the timer fired.
		if (_heardInput || State != OverlayState.Recording || !IsVisible || SilenceHint)
		{
			return;
		}

		SilenceHint = true;
		StateChanged?.Invoke(this, EventArgs.Empty);
	}

	private void DisarmSilenceHint()
	{
		_silenceTimer.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
		SilenceHint = false;
	}

	private void OnElapsedTick()
	{
		Elapsed = _timeProvider.GetUtcNow() - _recordingStartedAt;
		ElapsedChanged?.Invoke(this, EventArgs.Empty);

		// Self-reschedule: the tests' manual clock is one-shot, and a real timer is re-armed the same way.
		if (State == OverlayState.Recording)
		{
			_elapsedTimer.Change(ElapsedTick, Timeout.InfiniteTimeSpan);
		}
	}

	private void RaiseNearCap()
	{
		if (NearCap)
		{
			return;
		}

		NearCap = true;
		StateChanged?.Invoke(this, EventArgs.Empty);
	}

	private void RaiseError() => ShowBriefly(OverlayState.Error);

	// The recording ended with no audio at all: say so on the pill for a few seconds (WHISPER-144).
	private void RaiseNoAudio() => ShowBriefly(OverlayState.NoAudio);

	// Show a dictation outcome (an error, or no audio) on the pill, then dismiss it on its own timer.
	private void ShowBriefly(OverlayState state)
	{
		State = state;
		NearCap = false;
		_elapsedTimer.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
		DisarmSilenceHint();
		ResetLevel();

		SetVisible(true);
		StateChanged?.Invoke(this, EventArgs.Empty);
		PresentRequested?.Invoke(this, EventArgs.Empty);

		// Auto-dismiss so the overlay does not linger as a top-most window after the failure.
		_dismissTimer.Change(ErrorDismissAfter, Timeout.InfiniteTimeSpan);
	}

	private void OnErrorDismissed()
	{
		if (State is not (OverlayState.Error or OverlayState.NoAudio))
		{
			return;
		}

		NearCap = false;
		Elapsed = TimeSpan.Zero;

		// The error has run its course; show the warming pill if the model is still warming, otherwise hide.
		ShowWarmingOrHide();
	}

	// React to the app-wide warm-up signal. Warming is a BACKGROUND cue: if a dictation is
	// already using the pill (recording/transcribing, or an error on its dismiss timer), just remember the
	// flag — it is applied when that releases the pill (ReturnToRest / OnErrorDismissed). Otherwise reflect it
	// now. The "owned by a dictation" test is what is currently on the pill, not the recording state machine:
	// a failure arrives as a message without a state transition, so the machine can still read "Recording".
	private void OnWarmupChanged(bool isWarming)
	{
		if (_isWarming == isWarming)
		{
			return;
		}

		_isWarming = isWarming;

		if (IsVisible && State is OverlayState.Recording or OverlayState.Transcribing or OverlayState.Error or OverlayState.NoAudio)
		{
			return;
		}

		ShowWarmingOrHide();
	}

	// Show the warming pill while the model is warming, otherwise return the overlay to its hidden rest state.
	// Called only when no dictation owns the pill, so it never fights the recording feedback.
	private void ShowWarmingOrHide()
	{
		if (_isWarming)
		{
			State = OverlayState.Warming;
			ResetLevel();
			SetVisible(true);
		}
		else
		{
			State = OverlayState.Recording;
			SetVisible(false);
		}

		StateChanged?.Invoke(this, EventArgs.Empty);
	}

	private void SetVisible(bool visible)
	{
		if (visible == IsVisible)
		{
			return;
		}

		IsVisible = visible;
		VisibilityChanged?.Invoke(this, EventArgs.Empty);
	}

	private void ResetLevel()
	{
		if (Level == 0)
		{
			return;
		}

		Level = 0;
		LevelChanged?.Invoke(this, EventArgs.Empty);
	}

	// Map a frame's RMS to a perceptual 0-1 meter level: convert to dBFS and normalize the
	// MinimumDecibels..0 dBFS window to [0, 1]. Digital silence (rms <= 0) and anything below the floor
	// read 0; full digital scale (0 dBFS) reads 1.
	public static double ToPerceptualLevel(double rms)
	{
		if (rms <= 0)
		{
			return 0;
		}

		double decibels = 20 * Math.Log10(rms);
		return Math.Clamp((decibels - MinimumDecibels) / -MinimumDecibels, 0, 1);
	}

	// The RMS of a frame's float samples. Left unclamped: the perceptual mapping handles the range, and a
	// clipped frame (rms > 1) simply maps to full scale.
	private static double RootMeanSquare(ReadOnlySpan<float> samples)
	{
		if (samples.IsEmpty)
		{
			return 0;
		}

		double sumOfSquares = 0;
		foreach (float sample in samples)
		{
			sumOfSquares += (double)sample * sample;
		}

		return Math.Sqrt(sumOfSquares / samples.Length);
	}

	public void Dispose()
	{
		_messenger.UnregisterAll(this);
		_stateMachine.StateChanged -= OnStateChanged;
		_audioSource.FrameAvailable -= OnFrameAvailable;
		_elapsedTimer.Dispose();
		_dismissTimer.Dispose();
		_silenceTimer.Dispose();
	}
}
