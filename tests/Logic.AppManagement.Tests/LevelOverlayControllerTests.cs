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

	// --- WHISPER-101: perceptual (dBFS) mapping ---

	// Emit enough constant frames that the exponential smoothing has converged to the per-frame level, so
	// the resulting band can be asserted regardless of the smoothing factor.
	private void EmitSustained(float amplitude)
	{
		for (int i = 0; i < 60; i++)
		{
			EmitFrame(amplitude);
		}
	}

	[Theory]
	[InlineData(0.0)]      // digital silence
	[InlineData(0.0008)]   // ~-62 dBFS, below the floor
	public void Rms_at_or_below_the_floor_maps_to_zero(double rms) =>
		LevelOverlayController.ToPerceptualLevel(rms).Should().Be(0);

	[Theory]
	[InlineData(0.02)]   // ~-34 dBFS
	[InlineData(0.05)]   // ~-26 dBFS
	[InlineData(0.10)]   // ~-20 dBFS
	public void Normal_speech_rms_maps_to_mid_range(double rms) =>
		LevelOverlayController.ToPerceptualLevel(rms).Should().BeInRange(0.40, 0.70);

	[Fact]
	public void Mapping_is_monotonic_and_full_scale_reaches_the_top()
	{
		double quiet = LevelOverlayController.ToPerceptualLevel(0.01);
		double normal = LevelOverlayController.ToPerceptualLevel(0.05);
		double loud = LevelOverlayController.ToPerceptualLevel(0.5);

		quiet.Should().BeLessThan(normal);
		normal.Should().BeLessThan(loud);
		loud.Should().BeLessThan(1.0, "loud speech approaches full scale without pegging");
		LevelOverlayController.ToPerceptualLevel(1.0).Should().Be(1.0, "only full digital scale pegs the meter");
	}

	[Fact]
	public void Speaking_at_normal_volume_drives_the_meter_to_mid_range()
	{
		_stateMachine.RequestStart();

		EmitSustained(0.05f);

		_controller.Level.Should().BeInRange(0.40, 0.70, "normal-volume speech should fill the meter to mid-range");
	}

	[Fact]
	public void Silence_keeps_the_meter_at_or_near_zero()
	{
		_stateMachine.RequestStart();

		EmitSustained(0.0005f);

		_controller.Level.Should().BeLessThanOrEqualTo(0.02);
	}

	[Fact]
	public void Loud_speech_approaches_full_scale_without_pegging()
	{
		_stateMachine.RequestStart();

		EmitSustained(0.5f);

		_controller.Level.Should().BeGreaterThan(0.70, "loud speech approaches full scale");
		_controller.Level.Should().BeLessThan(1.0, "without constantly pegging");
	}
}
