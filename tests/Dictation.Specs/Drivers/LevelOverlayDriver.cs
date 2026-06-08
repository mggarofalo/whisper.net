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

	private void EmitInputFrame()
	{
		float[] loud = new float[480];
		Array.Fill(loud, 0.6f);
		_audioSource.FrameAvailable += Raise.EventWith(
			new AudioFrameAvailableEventArgs(loud, new CaptureFormat(16_000, 1, 32, AudioSampleFormat.IeeeFloat)));
	}

	// --- assertions ---

	public void AssertOverlayVisible() => _controller.IsVisible.Should().BeTrue();

	public void AssertOverlayHidden() => _controller.IsVisible.Should().BeFalse();

	public void AssertReflectsInputLevel() => _controller.Level.Should().BeGreaterThan(0);

	public void Dispose() => _controller.Dispose();
}
