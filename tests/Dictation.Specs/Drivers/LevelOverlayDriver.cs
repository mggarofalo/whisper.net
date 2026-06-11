// Drives the @WHISPER-26 level-overlay scenarios. It owns HOW the overlay is exercised so the steps stay
// one-liners: it builds the REAL LevelOverlayController over a real RecordingStateMachine and a faked
// audio source, drives recording start/stop on the state machine (as the orchestrator would), raises an
// audio frame to simulate speech, and asserts the controller's visibility and level. The controller is
// the unit-testable, WPF-free view-model logic the thin overlay binds to.

using Application.Ports;
using AwesomeAssertions;
using Domain.Audio;
using Logic.AppManagement;
using NSubstitute;

namespace Dictation.Specs.Drivers;

public sealed class LevelOverlayDriver : IDisposable
{
	private readonly RecordingStateMachine _stateMachine = new();
	private readonly IAudioSource _audioSource = Substitute.For<IAudioSource>();
	private readonly LevelOverlayController _controller;

	public LevelOverlayDriver() => _controller = new LevelOverlayController(_stateMachine, _audioSource);

	// Recording starts and a frame of speech-level audio flows, as it would during a real capture.
	public void StartRecording()
	{
		_stateMachine.RequestStart();
		EmitInputFrame();
	}

	public void StopRecording() => _stateMachine.RequestStop();

	// Start recording WITHOUT emitting a frame, so the @WHISPER-101 scenarios drive the meter purely with
	// the sustained audio they specify.
	public void BeginRecording() => _stateMachine.RequestStart();

	// Feed enough constant-amplitude frames that the exponential smoothing converges to the per-frame
	// perceptual level, so the resulting band can be asserted regardless of the smoothing factor.
	public void ReceiveSustainedAudio(float amplitude)
	{
		for (int i = 0; i < 60; i++)
		{
			EmitFrameAt(amplitude);
		}
	}

	private void EmitInputFrame() => EmitFrameAt(0.6f);

	private void EmitFrameAt(float amplitude)
	{
		float[] samples = new float[480];
		Array.Fill(samples, amplitude);
		_audioSource.FrameAvailable += Raise.EventWith(
			new AudioFrameAvailableEventArgs(samples, new CaptureFormat(16_000, 1, 32, AudioSampleFormat.IeeeFloat)));
	}

	// --- assertions ---

	public void AssertOverlayVisible() => _controller.IsVisible.Should().BeTrue();

	public void AssertOverlayHidden() => _controller.IsVisible.Should().BeFalse();

	public void AssertReflectsInputLevel() => _controller.Level.Should().BeGreaterThan(0);

	// WHISPER-101 perceptual-scale bands.
	public void AssertMeterMidRange() =>
		_controller.Level.Should().BeInRange(0.40, 0.70, "normal-volume speech should fill the meter to mid-range");

	public void AssertMeterNearZero() =>
		_controller.Level.Should().BeLessThanOrEqualTo(0.02, "silence sits at or near zero");

	public void AssertMeterApproachesFullWithoutPegging()
	{
		_controller.Level.Should().BeGreaterThan(0.70, "loud speech approaches full scale");
		_controller.Level.Should().BeLessThan(1.0, "without constantly pegging");
	}

	public void Dispose() => _controller.Dispose();
}
