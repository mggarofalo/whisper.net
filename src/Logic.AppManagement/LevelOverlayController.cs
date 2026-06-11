// The level overlay's coordination logic (WHISPER-26; feedback in WHISPER-102), kept out of Presentation
// so it can be driven for real in specs. It mirrors the live recording state (subscribing to the
// RecordingStateMachine the orchestrator drives) into an IsVisible flag and a presentation State
// (Recording / Transcribing / Error), turns the audio frames captured during recording into a smoothed,
// perceptual input level in [0, 1] the meter reflects (WHISPER-101), tracks the elapsed recording time on
// the injected TimeProvider, and listens on the shared IMessenger for the WHISPER-111 soft/hard limit
// signals (raising a near-cap warning before any audio could be lost) and for a dictation failure (a
// brief, auto-dismissed error state). The thin WPF overlay binds to this; it owns no UI itself, and the
// level math never touches or blocks the dictation pipeline (it only observes the frames the capture
// already raises).

using Application.Dictation;
using Application.Ports;
using CommunityToolkit.Mvvm.Messaging;
using Domain.Recording;

namespace Logic.AppManagement;

public sealed class LevelOverlayController : IDisposable
{
	// Exponential smoothing: how much each new frame moves the displayed level. Low enough that the meter
	// does not jitter frame-to-frame (no jank), high enough that it still feels live.
	private const double SmoothingFactor = 0.3;

	// The dBFS window the meter spans (WHISPER-101). Raw RMS is near 0 for silence and ~1 only at full
	// digital scale, but speech sits around 0.02-0.1 RMS, so a linear meter barely moves. Mapping RMS to
	// dBFS and normalizing this window to [0, 1] puts normal speech (~-26 dBFS) mid-bar, floors anything
	// below -60 dBFS at silence, and lets loud speech approach full scale without pegging unless it is
	// actually at 0 dBFS.
	private const double MinimumDecibels = -60.0;

	// How long the error state lingers before the overlay dismisses itself (WHISPER-102): long enough to
	// read, short enough not to nag in a windowless app.
	private static readonly TimeSpan ErrorDismissAfter = TimeSpan.FromSeconds(4);

	// The elapsed-time tick cadence. Self-rescheduled one-shot (re-armed each tick) so it advances both
	// under a real TimeProvider and under the tests' one-shot manual clock.
	private static readonly TimeSpan ElapsedTick = TimeSpan.FromSeconds(1);

	private readonly RecordingStateMachine _stateMachine;
	private readonly IAudioSource _audioSource;
	private readonly IMessenger _messenger;
	private readonly TimeProvider _timeProvider;
	private readonly ITimer _elapsedTimer;
	private readonly ITimer _dismissTimer;

	private DateTimeOffset _recordingStartedAt;

	public LevelOverlayController(
		RecordingStateMachine stateMachine,
		IAudioSource audioSource,
		IMessenger messenger,
		TimeProvider timeProvider)
	{
		_stateMachine = stateMachine;
		_audioSource = audioSource;
		_messenger = messenger;
		_timeProvider = timeProvider;

		IsVisible = stateMachine.State == RecordingState.Recording;
		State = OverlayState.Recording;

		_elapsedTimer = _timeProvider.CreateTimer(_ => OnElapsedTick(), null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
		_dismissTimer = _timeProvider.CreateTimer(_ => OnErrorDismissed(), null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);

		_stateMachine.StateChanged += OnStateChanged;
		_audioSource.FrameAvailable += OnFrameAvailable;

		// The WHISPER-111 soft/hard limit signals and the WHISPER-102 failure signal arrive on the shared
		// messenger (published by the orchestrator). Weak registration; removed on Dispose.
		_messenger.Register<LevelOverlayController, DictationNearLimitMessage>(this, (recipient, _) => recipient.RaiseNearCap());
		_messenger.Register<LevelOverlayController, DictationAtLimitMessage>(this, (recipient, _) => recipient.RaiseNearCap());
		_messenger.Register<LevelOverlayController, DictationHardLimitStopMessage>(this, (recipient, _) => recipient.RaiseNearCap());
		_messenger.Register<LevelOverlayController, DictationFailedMessage>(this, (recipient, _) => recipient.RaiseError());
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
	/// any audio could be lost (WHISPER-111). Reset when the next recording starts.</summary>
	public bool NearCap { get; private set; }

	/// <summary>Raised when <see cref="IsVisible"/> changes, so the view can show/hide the overlay.</summary>
	public event EventHandler? VisibilityChanged;

	/// <summary>Raised when <see cref="Level"/> changes, so the view can refresh the meter.</summary>
	public event EventHandler? LevelChanged;

	/// <summary>Raised when <see cref="State"/> or <see cref="NearCap"/> changes, so the view can restyle.</summary>
	public event EventHandler? StateChanged;

	/// <summary>Raised when <see cref="Elapsed"/> changes, so the view can refresh the timer text.</summary>
	public event EventHandler? ElapsedChanged;

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

		SetVisible(true);
		StateChanged?.Invoke(this, EventArgs.Empty);
		ElapsedChanged?.Invoke(this, EventArgs.Empty);
	}

	private void EnterTranscribing()
	{
		State = OverlayState.Transcribing;
		_elapsedTimer.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);

		// The meter is a recording cue; quiet it while transcribing so a stale level does not linger.
		ResetLevel();
		SetVisible(true);
		StateChanged?.Invoke(this, EventArgs.Empty);
	}

	private void ReturnToRest()
	{
		_elapsedTimer.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);

		// An error lingers (its own dismiss timer hides it); a normal return to Idle hides immediately.
		if (State == OverlayState.Error)
		{
			return;
		}

		ResetLevel();
		Elapsed = TimeSpan.Zero;
		NearCap = false;
		State = OverlayState.Recording;
		SetVisible(false);
		StateChanged?.Invoke(this, EventArgs.Empty);
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

	private void RaiseError()
	{
		State = OverlayState.Error;
		NearCap = false;
		_elapsedTimer.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
		ResetLevel();

		SetVisible(true);
		StateChanged?.Invoke(this, EventArgs.Empty);

		// Auto-dismiss so the overlay does not linger as a top-most window after the failure.
		_dismissTimer.Change(ErrorDismissAfter, Timeout.InfiniteTimeSpan);
	}

	private void OnErrorDismissed()
	{
		if (State != OverlayState.Error)
		{
			return;
		}

		State = OverlayState.Recording;
		NearCap = false;
		Elapsed = TimeSpan.Zero;
		SetVisible(false);
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

	// Map a frame's RMS to a perceptual 0-1 meter level (WHISPER-101): convert to dBFS and normalize the
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
	}
}
