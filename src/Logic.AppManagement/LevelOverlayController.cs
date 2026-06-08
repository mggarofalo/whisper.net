// The level overlay's coordination logic (WHISPER-26), kept out of Presentation so it can be driven for
// real in specs. It mirrors the live recording state (subscribing to the RecordingStateMachine the
// orchestrator drives) into an IsVisible flag — the mini-recorder is shown only while Recording — and
// turns the audio frames captured during recording into a smoothed input level in [0, 1] the meter
// reflects. The thin WPF overlay binds to this; it owns no UI itself, and the level math never touches
// or blocks the dictation pipeline (it only observes the frames the capture already raises).

using Application.Ports;
using Domain.Recording;

namespace Logic.AppManagement;

public sealed class LevelOverlayController : IDisposable
{
	private readonly RecordingStateMachine _stateMachine;
	private readonly IAudioSource _audioSource;

	// Exponential smoothing: how much each new frame moves the displayed level. Low enough that the meter
	// does not jitter frame-to-frame (no jank), high enough that it still feels live.
	private const double SmoothingFactor = 0.3;

	public LevelOverlayController(RecordingStateMachine stateMachine, IAudioSource audioSource)
	{
		_stateMachine = stateMachine;
		_audioSource = audioSource;
		IsVisible = stateMachine.State == RecordingState.Recording;
		_stateMachine.StateChanged += OnStateChanged;
		_audioSource.FrameAvailable += OnFrameAvailable;
	}

	/// <summary>Whether the overlay should be shown — true only while recording.</summary>
	public bool IsVisible { get; private set; }

	/// <summary>The smoothed microphone input level in [0, 1] the meter reflects.</summary>
	public double Level { get; private set; }

	/// <summary>Raised when <see cref="IsVisible"/> changes, so the view can show/hide the overlay.</summary>
	public event EventHandler? VisibilityChanged;

	/// <summary>Raised when <see cref="Level"/> changes, so the view can refresh the meter.</summary>
	public event EventHandler? LevelChanged;

	private void OnStateChanged(object? sender, RecordingStateChangedEventArgs e)
	{
		bool visible = e.Current == RecordingState.Recording;
		if (visible == IsVisible)
		{
			return;
		}

		IsVisible = visible;
		if (!visible)
		{
			// Reset the meter when recording stops so the next session starts from silence.
			Level = 0;
			LevelChanged?.Invoke(this, EventArgs.Empty);
		}

		VisibilityChanged?.Invoke(this, EventArgs.Empty);
	}

	private void OnFrameAvailable(object? sender, AudioFrameAvailableEventArgs e)
	{
		// Only meter while the overlay is showing; ignore the idle preroll frames the capture may raise.
		if (!IsVisible)
		{
			return;
		}

		double rms = RootMeanSquare(e.Samples.Span);
		Level = (SmoothingFactor * rms) + ((1 - SmoothingFactor) * Level);
		LevelChanged?.Invoke(this, EventArgs.Empty);
	}

	// Peak-normalized loudness of a frame: the RMS of its float samples, clamped to [0, 1] so the meter
	// maps directly to a 0-100% indicator without the view needing to know the audio scale.
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

		return Math.Clamp(Math.Sqrt(sumOfSquares / samples.Length), 0, 1);
	}

	public void Dispose()
	{
		_stateMachine.StateChanged -= OnStateChanged;
		_audioSource.FrameAvailable -= OnFrameAvailable;
	}
}
