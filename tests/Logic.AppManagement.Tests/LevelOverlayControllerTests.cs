// Unit depth for the WHISPER-26 level-overlay controller, beyond the @WHISPER-26 acceptance scenarios.
// Pins down show-on-record / hide-on-stop visibility, that the level only moves while recording, that
// louder audio reads higher than quieter audio (smoothed), and that the meter resets when recording
// stops. The audio source is an NSubstitute fake whose FrameAvailable the test raises directly.

using Application.Dictation;
using Application.Models;
using Application.Ports;
using AwesomeAssertions;
using CommunityToolkit.Mvvm.Messaging;
using Domain.Audio;
using Logic.AppManagement;
using Logic.AppManagement.Tests.Support;
using NSubstitute;
using Xunit;

namespace Logic.AppManagement.Tests;

public sealed class LevelOverlayControllerTests
{
	private readonly RecordingStateMachine _stateMachine = new();
	private readonly IAudioSource _audioSource = Substitute.For<IAudioSource>();
	private readonly IMessenger _messenger = new WeakReferenceMessenger();
	private readonly ManualTimeProvider _time = new();
	private readonly LevelOverlayController _controller;

	public LevelOverlayControllerTests() => _controller = new LevelOverlayController(_stateMachine, _audioSource, _messenger, _time);

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
	public void Becomes_visible_while_recording_stays_visible_transcribing_and_hides_when_idle()
	{
		_stateMachine.RequestStart();
		_controller.IsVisible.Should().BeTrue();
		_controller.State.Should().Be(OverlayState.Recording);

		// Stop -> Transcribing: the overlay stays up so the user sees the transcribe step (WHISPER-102).
		_stateMachine.RequestStop();
		_controller.IsVisible.Should().BeTrue();
		_controller.State.Should().Be(OverlayState.Transcribing);

		// Complete -> Idle: only now does the overlay hide.
		_stateMachine.CompleteTranscription();
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

	// --- WHISPER-102: state, elapsed, near-cap, and error feedback ---

	[Fact]
	public void Returning_to_idle_resets_state_and_hides()
	{
		_stateMachine.RequestStart();
		_stateMachine.RequestStop();
		_stateMachine.CompleteTranscription();

		_controller.IsVisible.Should().BeFalse();
		_controller.NearCap.Should().BeFalse();
		_controller.Elapsed.Should().Be(TimeSpan.Zero);
	}

	[Fact]
	public void Elapsed_time_advances_while_recording()
	{
		_stateMachine.RequestStart();

		_time.Advance(TimeSpan.FromSeconds(3));

		_controller.Elapsed.Should().BeGreaterThanOrEqualTo(TimeSpan.FromSeconds(3));
	}

	[Fact]
	public void Elapsed_resets_when_a_new_recording_starts()
	{
		_stateMachine.RequestStart();
		_time.Advance(TimeSpan.FromSeconds(5));
		_stateMachine.RequestStop();
		_stateMachine.CompleteTranscription();

		_stateMachine.RequestStart();

		_controller.Elapsed.Should().Be(TimeSpan.Zero);
	}

	[Fact]
	public void A_near_limit_message_raises_the_near_cap_warning()
	{
		_stateMachine.RequestStart();

		_messenger.Send(new DictationNearLimitMessage(8000, 10000));

		_controller.NearCap.Should().BeTrue();
	}

	[Fact]
	public void An_at_limit_message_raises_the_near_cap_warning()
	{
		_stateMachine.RequestStart();

		_messenger.Send(new DictationAtLimitMessage(10000, 10000));

		_controller.NearCap.Should().BeTrue();
	}

	[Fact]
	public void A_hard_limit_stop_message_raises_the_near_cap_warning()
	{
		_stateMachine.RequestStart();

		_messenger.Send(new DictationHardLimitStopMessage(20000, 20000));

		_controller.NearCap.Should().BeTrue();
	}

	[Fact]
	public void Near_cap_resets_on_the_next_recording()
	{
		_stateMachine.RequestStart();
		_messenger.Send(new DictationAtLimitMessage(10000, 10000));
		_controller.NearCap.Should().BeTrue();
		_stateMachine.RequestStop();
		_stateMachine.CompleteTranscription();

		_stateMachine.RequestStart();

		_controller.NearCap.Should().BeFalse();
	}

	[Fact]
	public void A_failure_message_shows_the_error_state_and_keeps_the_overlay_visible()
	{
		_stateMachine.RequestStart();

		_messenger.Send(new DictationFailedMessage());

		_controller.State.Should().Be(OverlayState.Error);
		_controller.IsVisible.Should().BeTrue();
	}

	[Fact]
	public void The_error_state_auto_dismisses_after_the_timeout()
	{
		_stateMachine.RequestStart();
		_messenger.Send(new DictationFailedMessage());
		_controller.IsVisible.Should().BeTrue();

		_time.Advance(TimeSpan.FromSeconds(5));

		_controller.IsVisible.Should().BeFalse();
		_controller.State.Should().Be(OverlayState.Recording);
	}

	[Fact]
	public void An_error_lingers_past_the_return_to_idle_until_it_dismisses()
	{
		_stateMachine.RequestStart();
		_stateMachine.RequestStop();          // Transcribing
		_messenger.Send(new DictationFailedMessage());
		_stateMachine.CompleteTranscription(); // Idle — but the error must linger, not vanish

		_controller.IsVisible.Should().BeTrue("the error lingers until its dismiss timeout, not the return to Idle");
		_controller.State.Should().Be(OverlayState.Error);

		_time.Advance(TimeSpan.FromSeconds(5));

		_controller.IsVisible.Should().BeFalse();
	}

	// --- WHISPER-129: model warm-up status ---

	[Fact]
	public void A_warm_up_started_message_shows_the_overlay_in_the_warming_state()
	{
		_messenger.Send(new ModelWarmupChangedMessage(true));

		_controller.IsVisible.Should().BeTrue();
		_controller.State.Should().Be(OverlayState.Warming);
	}

	[Fact]
	public void The_warm_up_cleared_message_hides_the_overlay()
	{
		_messenger.Send(new ModelWarmupChangedMessage(true));
		_messenger.Send(new ModelWarmupChangedMessage(false));

		_controller.IsVisible.Should().BeFalse();
	}

	[Fact]
	public void A_recording_takes_precedence_over_a_concurrent_warm_up()
	{
		_messenger.Send(new ModelWarmupChangedMessage(true));   // the pill is showing "warming up"
		_stateMachine.RequestStart();                           // a recording begins mid warm-up

		_controller.State.Should().Be(OverlayState.Recording, "an active recording owns the pill, not the warm-up cue");
		_controller.IsVisible.Should().BeTrue();
	}

	[Fact]
	public void The_warming_pill_returns_after_a_recording_when_the_warm_up_is_still_running()
	{
		_messenger.Send(new ModelWarmupChangedMessage(true));
		_stateMachine.RequestStart();
		_stateMachine.RequestStop();
		_stateMachine.CompleteTranscription();   // back to idle, but the warm-up never cleared

		_controller.State.Should().Be(OverlayState.Warming);
		_controller.IsVisible.Should().BeTrue();
	}

	[Fact]
	public void A_warm_up_that_clears_during_a_recording_leaves_nothing_lingering_after_it()
	{
		_stateMachine.RequestStart();
		_messenger.Send(new ModelWarmupChangedMessage(true));
		_messenger.Send(new ModelWarmupChangedMessage(false));  // warm-up finished while recording
		_stateMachine.RequestStop();
		_stateMachine.CompleteTranscription();

		_controller.IsVisible.Should().BeFalse("the warm-up cleared while recording, so no warming pill lingers after it");
	}
}
