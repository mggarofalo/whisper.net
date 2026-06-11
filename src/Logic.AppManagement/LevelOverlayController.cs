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

	// The dBFS window the meter spans (WHISPER-101). Raw RMS is near 0 for silence and ~1 only at full
	// digital scale, but speech sits around 0.02-0.1 RMS, so a linear meter barely moves. Mapping RMS to
	// dBFS and normalizing this window to [0, 1] puts normal speech (~-26 dBFS) mid-bar, floors anything
	// below -60 dBFS at silence, and lets loud speech approach full scale without pegging unless it is
	// actually at 0 dBFS.
	private const double MinimumDecibels = -60.0;

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
		double perceptual = ToPerceptualLevel(rms);
		Level = (SmoothingFactor * perceptual) + ((1 - SmoothingFactor) * Level);
		LevelChanged?.Invoke(this, EventArgs.Empty);
	}

	// Map a frame's RMS to a perceptual 0-1 meter level (WHISPER-101): convert to dBFS and normalize the
	// MinimumDecibels..0 dBFS window to [0, 1]. Digital silence (rms <= 0) and anything below the floor
	// read 0; full digital scale (0 dBFS) reads 1. This is what makes normal speech land mid-bar instead
	// of a couple of unreadable pixels.
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
		_stateMachine.StateChanged -= OnStateChanged;
		_audioSource.FrameAvailable -= OnFrameAvailable;
	}
}
