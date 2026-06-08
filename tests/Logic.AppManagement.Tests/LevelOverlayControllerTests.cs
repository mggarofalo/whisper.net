// Unit depth for the WHISPER-26 level-overlay controller, beyond the @WHISPER-26 acceptance scenarios.
// Pins down show-on-record / hide-on-stop visibility, that the level only moves while recording, that
// louder audio reads higher than quieter audio (smoothed), and that the meter resets when recording
// stops. The audio source is an NSubstitute fake whose FrameAvailable the test raises directly.

using Application.Ports;
using AwesomeAssertions;
using Domain.Audio;
using Logic.AppManagement;
using NSubstitute;
using Xunit;

namespace Logic.AppManagement.Tests;

public sealed class LevelOverlayControllerTests
{
	private readonly RecordingStateMachine _stateMachine = new();
	private readonly IAudioSource _audioSource = Substitute.For<IAudioSource>();
	private readonly LevelOverlayController _controller;

	public LevelOverlayControllerTests() => _controller = new LevelOverlayController(_stateMachine, _audioSource);

	private void EmitFrame(float amplitude)
	{
		float[] samples = new float[480];
		Array.Fill(samples, amplitude);
		_audioSource.FrameAvailable += Raise.EventWith(
			new AudioFrameAvailableEventArgs(samples, new CaptureFormat(16_000, 1, 32, AudioSampleFormat.IeeeFloat)));
	}

	[Fact]
	public void Starts_hidden()
	{
		_controller.IsVisible.Should().BeFalse();
		_controller.Level.Should().Be(0);
	}

	[Fact]
	public void Becomes_visible_while_recording_and_hides_when_recording_stops()
	{
		_stateMachine.RequestStart();
		_controller.IsVisible.Should().BeTrue();

		_stateMachine.RequestStop();
		_controller.IsVisible.Should().BeFalse();
	}

	[Fact]
	public void Reflects_input_level_while_recording()
	{
		_stateMachine.RequestStart();

		EmitFrame(0.5f);

		_controller.Level.Should().BeGreaterThan(0);
	}

	[Fact]
	public void Ignores_frames_when_not_recording()
	{
		EmitFrame(0.9f);

		_controller.Level.Should().Be(0);
	}

	[Fact]
	public void Louder_audio_reads_higher_than_quieter_audio()
	{
		_stateMachine.RequestStart();

		EmitFrame(0.1f);
		double quiet = _controller.Level;

		// Several loud frames so the smoothed level climbs above the quiet reading.
		EmitFrame(0.8f);
		EmitFrame(0.8f);
		EmitFrame(0.8f);

		_controller.Level.Should().BeGreaterThan(quiet);
	}

	[Fact]
	public void Resets_the_meter_when_recording_stops()
	{
		_stateMachine.RequestStart();
		EmitFrame(0.8f);

		_stateMachine.RequestStop();

		_controller.Level.Should().Be(0);
	}
}
