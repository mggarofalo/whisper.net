// Unit depth for the dictation orchestrator, beyond the acceptance scenarios.
// Pins down the explicit stage path (Idle -> Recording -> Transcribing -> Delivering -> Idle) and its
// observability, the hotkey start signal, the concurrency guard against a second capture, the Esc
// cancel that discards a capture, and the two error paths (a failed delivery and a device capture
// failure) that must log and return the pipeline to a safe Idle. The history write-through
// is pinned here too: a delivered result dispatches a RecordTranscriptionCommand, an undelivered one
// does not, and a failed history write is swallowed with a warning. The post-release grace window
// is pinned on a manual clock: delivery waits for the window, frames arriving during it
// land in the delivered clip, a cancel mid-grace cannot derail the stop, a cancelled wait discards
// the capture, and a device failure mid-grace — including one landing at the grace boundary, racing
// the finalization — discards the capture, notifies the user, and never poisons the next utterance.
// The soft recording limit is pinned here at the orchestration boundary: approaching the
// limit publishes DictationNearLimitMessage once, reaching it publishes DictationAtLimitMessage once,
// and frames past the limit still land in the delivered clip. The hard failsafe is pinned too: a
// recording reaching the hard ceiling publishes DictationHardLimitStopMessage once and stops ITSELF
// through the normal stop path — the clip reaches delivery, nothing is discarded, and the pipeline
// returns to Idle. Every port is an NSubstitute fake (the messenger is a real WeakReferenceMessenger,
// the repo standard), so the orchestration is exercised with no real audio, model, or delivery.

using Application.Configuration;
using Application.Dictation;
using Application.History;
using Application.Ports;
using Application.Transcription;
using AwesomeAssertions;
using CommunityToolkit.Mvvm.Messaging;
using Domain.Audio;
using Domain.Feedback;
using Domain.Input;
using Domain.Recording;
using Domain.Settings;
using Logic.AppManagement.Tests.Support;
using Logic.AudioManagement;
using Mediator;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace Logic.AppManagement.Tests;

public sealed class DictationOrchestratorTests
{
	private readonly IAudioSource _audio = Substitute.For<IAudioSource>();
	private readonly IMediator _mediator = Substitute.For<IMediator>();
	private readonly RecordingStateMachine _stateMachine = new();
	private readonly HotkeyActivationController _activation = new();
	private readonly IAudioFeedback _feedback = Substitute.For<IAudioFeedback>();
	private readonly WeakReferenceMessenger _messenger = new();
	private readonly AudioFeedbackOptions _feedbackOptions = new();
	private readonly IUserNotifier _userNotifier = Substitute.For<IUserNotifier>();
	private readonly CapturingLogger<DictationOrchestrator> _logger = new();

	public DictationOrchestratorTests() =>
		// Default the delivery pipeline to a successful result so the non-error tests don't hit the catch.
		_mediator
			.Send(Arg.Any<DeliverTranscriptionCommand>(), Arg.Any<CancellationToken>())
			.Returns(new DeliveryResult(Delivered: true, Text: "the result"));

	// Grace 0 by default so the stage/delivery tests stay synchronous; the grace-window
	// tests opt in with a positive window driven by a manual clock.
	private DictationOrchestrator CreateSut(AudioBufferingOptions? bufferingOptions = null, TimeProvider? time = null) =>
		new(_audio, _stateMachine, _activation, new AudioResampler(),
			bufferingOptions ?? new AudioBufferingOptions(PostReleaseGraceMs: 0), _mediator,
			_messenger, _feedback, Options.Create(_feedbackOptions), _userNotifier,
			time ?? TimeProvider.System, _logger);

	[Fact]
	public void Starts_idle()
	{
		DictationOrchestrator sut = CreateSut();

		sut.Stage.Should().Be(DictationStage.Idle);
	}

	[Fact]
	public void Start_enters_recording_and_begins_capture()
	{
		DictationOrchestrator sut = CreateSut();

		sut.Start();

		sut.Stage.Should().Be(DictationStage.Recording);
		_stateMachine.State.Should().Be(RecordingState.Recording);
		_audio.Received(1).Start();
	}

	[Fact]
	public void A_second_start_while_recording_does_not_open_a_second_capture()
	{
		DictationOrchestrator sut = CreateSut();

		sut.Start();
		sut.Start();

		_audio.Received(1).Start();
	}

	[Fact]
	public void A_hotkey_press_starts_recording()
	{
		DictationOrchestrator sut = CreateSut();
		_activation.Configure(HotkeyBinding.FromKeys(KeyModifiers.None, KeyboardKey.F13), ActivationMode.PushToTalk);

		_activation.HandleKeyDown(KeyboardKey.F13, KeyModifiers.None);

		sut.Stage.Should().Be(DictationStage.Recording);
	}

	[Fact]
	public async Task A_full_cycle_runs_delivery_and_returns_to_idle()
	{
		DictationOrchestrator sut = CreateSut();

		sut.Start();
		await sut.StopAsync(TestContext.Current.CancellationToken);

		await _mediator.Received(1).Send(Arg.Any<DeliverTranscriptionCommand>(), Arg.Any<CancellationToken>());
		_audio.Received(1).Stop();
		sut.Stage.Should().Be(DictationStage.Idle);
		_stateMachine.State.Should().Be(RecordingState.Idle);
	}

	[Fact]
	public async Task A_stop_while_idle_is_ignored()
	{
		DictationOrchestrator sut = CreateSut();

		await sut.StopAsync(TestContext.Current.CancellationToken);

		await _mediator.DidNotReceive().Send(Arg.Any<DeliverTranscriptionCommand>(), Arg.Any<CancellationToken>());
		sut.Stage.Should().Be(DictationStage.Idle);
	}

	[Fact]
	public async Task The_stage_path_is_observable_across_a_full_cycle()
	{
		DictationOrchestrator sut = CreateSut();
		List<DictationStage> observed = [];
		sut.StageChanged += (_, e) => observed.Add(e.Current);

		sut.Start();
		await sut.StopAsync(TestContext.Current.CancellationToken);

		observed.Should().Equal(
			DictationStage.Recording,
			DictationStage.Transcribing,
			DictationStage.Delivering,
			DictationStage.Idle);
	}

	[Fact]
	public async Task A_delivery_failure_is_logged_and_returns_to_idle()
	{
		_mediator
			.Send(Arg.Any<DeliverTranscriptionCommand>(), Arg.Any<CancellationToken>())
			.Returns<DeliveryResult>(_ => throw new InvalidOperationException("delivery failed"));
		DictationOrchestrator sut = CreateSut();

		sut.Start();
		await sut.StopAsync(TestContext.Current.CancellationToken);

		sut.Stage.Should().Be(DictationStage.Idle);
		_stateMachine.State.Should().Be(RecordingState.Idle);
		_logger.Entries.Should().Contain(entry => entry.Level == LogLevel.Error);
	}

	[Fact]
	public async Task A_delivered_transcription_is_recorded_to_history()
	{
		DictationOrchestrator sut = CreateSut();

		sut.Start();
		await sut.StopAsync(TestContext.Current.CancellationToken);

		await _mediator.Received(1).Send(
			Arg.Is<RecordTranscriptionCommand>(command =>
				command.Text == "the result" && command.Duration >= TimeSpan.Zero),
			Arg.Any<CancellationToken>());
	}

	[Fact]
	public async Task A_clip_without_a_usable_sample_rate_records_a_zero_duration_instead_of_failing()
	{
		// A non-positive target rate makes the finalized clip's SampleRate 0; the duration guard must
		// yield TimeSpan.Zero rather than let the NaN division throw before the pipeline even runs.
		DictationOrchestrator sut = CreateSut(new AudioBufferingOptions(TargetSampleRate: 0, PostReleaseGraceMs: 0));

		sut.Start();
		await sut.StopAsync(TestContext.Current.CancellationToken);

		await _mediator.Received(1).Send(
			Arg.Is<RecordTranscriptionCommand>(command => command.Duration == TimeSpan.Zero),
			Arg.Any<CancellationToken>());
	}

	[Fact]
	public async Task An_undelivered_result_is_not_recorded_to_history()
	{
		_mediator
			.Send(Arg.Any<DeliverTranscriptionCommand>(), Arg.Any<CancellationToken>())
			.Returns(new DeliveryResult(Delivered: false, Text: string.Empty));
		DictationOrchestrator sut = CreateSut();

		sut.Start();
		await sut.StopAsync(TestContext.Current.CancellationToken);

		await _mediator.DidNotReceive().Send(Arg.Any<RecordTranscriptionCommand>(), Arg.Any<CancellationToken>());
	}

	[Fact]
	public async Task A_history_write_failure_is_swallowed_with_a_warning_and_never_breaks_delivery()
	{
		_mediator
			.Send(Arg.Any<RecordTranscriptionCommand>(), Arg.Any<CancellationToken>())
			.Returns<Mediator.Unit>(_ => throw new InvalidOperationException("history write failed"));
		DictationOrchestrator sut = CreateSut();

		sut.Start();
		await sut.StopAsync(TestContext.Current.CancellationToken);

		// The pipeline completed (no error path taken) and the failure was downgraded to a warning.
		sut.Stage.Should().Be(DictationStage.Idle);
		_stateMachine.State.Should().Be(RecordingState.Idle);
		_logger.Entries.Should().Contain(entry => entry.Level == LogLevel.Warning);
		_logger.Entries.Should().NotContain(entry => entry.Level == LogLevel.Error);
	}

	[Fact]
	public async Task Frames_arriving_during_the_post_release_grace_window_land_in_the_delivered_clip()
	{
		// The device's stop is asynchronous: the user's final syllables arrive after the
		// stop request. They must drain into the delivered clip, not the idle preroll ring.
		ManualTimeProvider time = new();
		AudioClip? delivered = null;
		_mediator
			.Send(Arg.Do<DeliverTranscriptionCommand>(command => delivered = command.Clip), Arg.Any<CancellationToken>())
			.Returns(new DeliveryResult(Delivered: true, Text: "the result"));
		DictationOrchestrator sut = CreateSut(new AudioBufferingOptions(PrerollMs: 0, PostReleaseGraceMs: 400), time);

		sut.Start();
		RaiseFrame(0.5f);
		Task stop = sut.StopAsync(TestContext.Current.CancellationToken);
		RaiseFrame(0.7f); // the flush the device delivers after the stop request
		time.Advance(TimeSpan.FromMilliseconds(400));
		await stop;

		delivered.Should().NotBeNull();
		delivered!.Samples.Should().Contain(0.5f);
		delivered.Samples.Should().Contain(0.7f);
	}

	[Fact]
	public async Task Delivery_waits_for_the_post_release_grace_window_to_elapse()
	{
		ManualTimeProvider time = new();
		DictationOrchestrator sut = CreateSut(new AudioBufferingOptions(PostReleaseGraceMs: 400), time);

		sut.Start();
		Task stop = sut.StopAsync(TestContext.Current.CancellationToken);

		stop.IsCompleted.Should().BeFalse("the stop holds the capture open until the grace window elapses");
		await _mediator.DidNotReceive().Send(Arg.Any<DeliverTranscriptionCommand>(), Arg.Any<CancellationToken>());

		time.Advance(TimeSpan.FromMilliseconds(400));
		await stop;

		await _mediator.Received(1).Send(Arg.Any<DeliverTranscriptionCommand>(), Arg.Any<CancellationToken>());
		sut.Stage.Should().Be(DictationStage.Idle);
	}

	[Fact]
	public async Task A_cancel_during_the_grace_window_does_not_derail_the_stop_in_flight()
	{
		// Esc lands between release and finalization: the pipeline is already Transcribing, so the
		// cancel is ignored and the stop still delivers exactly one (non-empty) clip.
		ManualTimeProvider time = new();
		DictationOrchestrator sut = CreateSut(new AudioBufferingOptions(PostReleaseGraceMs: 400), time);

		sut.Start();
		Task stop = sut.StopAsync(TestContext.Current.CancellationToken);
		sut.Cancel();
		time.Advance(TimeSpan.FromMilliseconds(400));
		await stop;

		await _mediator.Received(1).Send(Arg.Any<DeliverTranscriptionCommand>(), Arg.Any<CancellationToken>());
		sut.Stage.Should().Be(DictationStage.Idle);
	}

	[Fact]
	public async Task A_stop_cancelled_during_the_grace_window_discards_the_capture_and_returns_to_idle()
	{
		ManualTimeProvider time = new();
		using CancellationTokenSource cancellation = new();
		DictationOrchestrator sut = CreateSut(new AudioBufferingOptions(PostReleaseGraceMs: 400), time);

		sut.Start();
		Task stop = sut.StopAsync(cancellation.Token);
		await cancellation.CancelAsync();
		await stop;

		await _mediator.DidNotReceive().Send(Arg.Any<DeliverTranscriptionCommand>(), Arg.Any<CancellationToken>());
		sut.Stage.Should().Be(DictationStage.Idle);
		_stateMachine.State.Should().Be(RecordingState.Idle);
	}

	[Fact]
	public async Task A_capture_device_failure_during_the_grace_window_discards_the_capture_and_notifies()
	{
		// The device dies after release but before the grace elapses: the failure must be
		// logged and surfaced exactly like a Recording-stage failure, and the in-flight stop must discard
		// the partial capture — no delivery, no history entry — and return the pipeline to a safe Idle.
		ManualTimeProvider time = new();
		DictationOrchestrator sut = CreateSut(new AudioBufferingOptions(PostReleaseGraceMs: 400), time);

		sut.Start();
		Task stop = sut.StopAsync(TestContext.Current.CancellationToken);
		_audio.CaptureFailed += Raise.EventWith(
			new AudioCaptureFailedEventArgs(Domain.Audio.AudioCaptureError.DeviceUnavailable, "device removed"));
		time.Advance(TimeSpan.FromMilliseconds(400));
		await stop;

		await _mediator.DidNotReceive().Send(Arg.Any<DeliverTranscriptionCommand>(), Arg.Any<CancellationToken>());
		await _mediator.DidNotReceive().Send(Arg.Any<RecordTranscriptionCommand>(), Arg.Any<CancellationToken>());
		sut.Stage.Should().Be(DictationStage.Idle);
		_stateMachine.State.Should().Be(RecordingState.Idle);
		_logger.Entries.Should().Contain(entry => entry.Level == LogLevel.Error);
		_userNotifier.Received(1).NotifyError(Arg.Any<string>(), Arg.Any<string>());
	}

	[Fact]
	public async Task A_dictation_after_a_mid_grace_capture_failure_delivers_normally()
	{
		// The failure signal is scoped to the in-flight stop: Start resets it, so the next utterance
		// must run the full pipeline as if the earlier device failure had never happened.
		ManualTimeProvider time = new();
		DictationOrchestrator sut = CreateSut(new AudioBufferingOptions(PostReleaseGraceMs: 400), time);

		sut.Start();
		Task failedStop = sut.StopAsync(TestContext.Current.CancellationToken);
		_audio.CaptureFailed += Raise.EventWith(
			new AudioCaptureFailedEventArgs(Domain.Audio.AudioCaptureError.DeviceUnavailable, "device removed"));
		time.Advance(TimeSpan.FromMilliseconds(400));
		await failedStop;

		sut.Start();
		Task stop = sut.StopAsync(TestContext.Current.CancellationToken);
		time.Advance(TimeSpan.FromMilliseconds(400));
		await stop;

		await _mediator.Received(1).Send(Arg.Any<DeliverTranscriptionCommand>(), Arg.Any<CancellationToken>());
		sut.Stage.Should().Be(DictationStage.Idle);
		_stateMachine.State.Should().Be(RecordingState.Idle);
	}

	[Fact]
	public async Task A_capture_failure_racing_the_post_grace_finalization_discards_the_capture()
	{
		// The late-failure race the merged final gate guards: OnCaptureFailed fires in the
		// gap between the grace delay completing and the stop finalizing the capture. Code that read the
		// failure flag once, ahead of a separate stage guard, could notify the user the microphone failed
		// and then deliver the partial clip anyway; the single final gate re-reads the flag immediately
		// before finalization, so the gap shrinks to one read. The true interleaving has no seam a test
		// can drive: with the manual clock the awaited delay resumes — and the whole stop completes —
		// synchronously inside Advance, so a failure raised after Advance returns models a failure after
		// delivery, not one inside the gap (verified: the stop task is already complete when Advance
		// returns). The closest deterministic approximation, used here, hooks the failure raise to the
		// same Advance tick through a manual timer due at the grace boundary and registered ahead of the
		// stop's own delay timer: the failure lands inside Advance immediately before the gate runs.
		// This interleaving is caught by the pre-merge code too — the race window sits between two
		// adjacent reads and cannot be entered on demand — so this test pins the merged-gate behavior
		// rather than reproducing the bug.
		ManualTimeProvider time = new();
		DictationOrchestrator sut = CreateSut(new AudioBufferingOptions(PostReleaseGraceMs: 400), time);

		sut.Start();
		using ITimer failure = time.CreateTimer(
			_ => _audio.CaptureFailed += Raise.EventWith(
				new AudioCaptureFailedEventArgs(Domain.Audio.AudioCaptureError.DeviceUnavailable, "device removed")),
			null, TimeSpan.FromMilliseconds(400), Timeout.InfiniteTimeSpan);
		Task stop = sut.StopAsync(TestContext.Current.CancellationToken);
		time.Advance(TimeSpan.FromMilliseconds(400));
		await stop;

		await _mediator.DidNotReceive().Send(Arg.Any<DeliverTranscriptionCommand>(), Arg.Any<CancellationToken>());
		await _mediator.DidNotReceive().Send(Arg.Any<RecordTranscriptionCommand>(), Arg.Any<CancellationToken>());
		sut.Stage.Should().Be(DictationStage.Idle);
		_stateMachine.State.Should().Be(RecordingState.Idle);
		_logger.Entries.Should().Contain(entry => entry.Level == LogLevel.Error);
		_userNotifier.Received(1).NotifyError(Arg.Any<string>(), Arg.Any<string>());
	}

	[Fact]
	public void Approaching_the_soft_limit_publishes_a_near_limit_message_once()
	{
		// 100 ms soft limit at 16 kHz = 1600 samples; the 80% near threshold is 1280 = 8 frames of 160.
		List<DictationNearLimitMessage> nearLimit = [];
		List<DictationAtLimitMessage> atLimit = [];
		_messenger.Register<DictationNearLimitMessage>(this, (_, message) => nearLimit.Add(message));
		_messenger.Register<DictationAtLimitMessage>(this, (_, message) => atLimit.Add(message));
		DictationOrchestrator sut = CreateSut(new AudioBufferingOptions(PrerollMs: 0, MaxDurationMs: 100, PostReleaseGraceMs: 0));

		sut.Start();
		for (int i = 0; i < 9; i++)
		{
			RaiseFrame(0.5f); // 90 ms: through the 80% threshold, still below the limit
		}

		nearLimit.Should().ContainSingle("the warning fires once per recording, not once per frame");
		nearLimit[0].RecordedMs.Should().Be(80, "the message reports how much was recorded when the threshold was crossed");
		nearLimit[0].LimitMs.Should().Be(100);
		atLimit.Should().BeEmpty("the recording has not reached the limit yet");
	}

	[Fact]
	public void Reaching_the_soft_limit_publishes_an_at_limit_message_once()
	{
		List<DictationNearLimitMessage> nearLimit = [];
		List<DictationAtLimitMessage> atLimit = [];
		_messenger.Register<DictationNearLimitMessage>(this, (_, message) => nearLimit.Add(message));
		_messenger.Register<DictationAtLimitMessage>(this, (_, message) => atLimit.Add(message));
		DictationOrchestrator sut = CreateSut(new AudioBufferingOptions(PrerollMs: 0, MaxDurationMs: 100, PostReleaseGraceMs: 0));

		sut.Start();
		for (int i = 0; i < 12; i++)
		{
			RaiseFrame(0.5f); // 120 ms: through the 80% threshold and past the limit
		}

		nearLimit.Should().ContainSingle();
		atLimit.Should().ContainSingle("the at-limit signal fires once per recording even as frames keep arriving");
		atLimit[0].RecordedMs.Should().Be(100, "the message reports the recording length at the limit");
		atLimit[0].LimitMs.Should().Be(100);
	}

	[Fact]
	public async Task Frames_past_the_soft_limit_land_in_the_delivered_clip()
	{
		// The limit is soft: the recording grows past it, so the whole utterance —
		// 120 ms against a 100 ms limit — must reach delivery, never a truncated 100 ms clip.
		AudioClip? delivered = null;
		_mediator
			.Send(Arg.Do<DeliverTranscriptionCommand>(command => delivered = command.Clip), Arg.Any<CancellationToken>())
			.Returns(new DeliveryResult(Delivered: true, Text: "the result"));
		DictationOrchestrator sut = CreateSut(new AudioBufferingOptions(PrerollMs: 0, MaxDurationMs: 100, PostReleaseGraceMs: 0));

		sut.Start();
		for (int i = 0; i < 12; i++)
		{
			RaiseFrame(0.5f);
		}

		await sut.StopAsync(TestContext.Current.CancellationToken);

		delivered.Should().NotBeNull();
		delivered!.Samples.Should().HaveCount(12 * 160, "every frame, including those past the soft limit, is retained");
	}

	[Fact]
	public void Reaching_the_hard_limit_stops_and_transcribes_the_recording()
	{
		// The hard failsafe: with no UI consuming the soft-limit warnings yet, a runaway
		// recording must stop itself at the hard ceiling THROUGH THE NORMAL STOP PATH — the clip reaches
		// delivery, nothing is discarded — and the pipeline returns to Idle.
		// 200 ms hard limit at 16 kHz = 3200 samples = 20 frames of 160.
		List<DictationHardLimitStopMessage> hardLimit = [];
		_messenger.Register<DictationHardLimitStopMessage>(this, (_, message) => hardLimit.Add(message));
		AudioClip? delivered = null;
		_mediator
			.Send(Arg.Do<DeliverTranscriptionCommand>(command => delivered = command.Clip), Arg.Any<CancellationToken>())
			.Returns(new DeliveryResult(Delivered: true, Text: "the result"));
		DictationOrchestrator sut = CreateSut(new AudioBufferingOptions(
			PrerollMs: 0, MaxDurationMs: 100, PostReleaseGraceMs: 0, HardMaxDurationMs: 200));

		sut.Start();
		for (int i = 0; i < 20; i++)
		{
			RaiseFrame(0.5f); // the 20th frame reaches the 200 ms hard ceiling and triggers the auto-stop
		}

		hardLimit.Should().ContainSingle("the failsafe stop is signalled exactly once");
		hardLimit[0].RecordedMs.Should().Be(200, "the message reports the recording length at the hard ceiling");
		hardLimit[0].LimitMs.Should().Be(200);
		delivered.Should().NotBeNull("the hard-limit stop transcribes the recording instead of discarding it");
		delivered!.Samples.Should().HaveCount(20 * 160, "every sample recorded up to the hard limit reaches delivery");
		sut.Stage.Should().Be(DictationStage.Idle);
		_stateMachine.State.Should().Be(RecordingState.Idle);
	}

	[Fact]
	public async Task Frames_arriving_after_the_hard_limit_stop_neither_restart_nor_duplicate_the_stop()
	{
		// The capture device keeps producing while the auto-stop drains: the late frames must not
		// re-trigger the failsafe or open a second delivery — StopAsync's entry transition makes any
		// duplicate a no-op, and the hard-limit signal is armed once per recording.
		List<DictationHardLimitStopMessage> hardLimit = [];
		_messenger.Register<DictationHardLimitStopMessage>(this, (_, message) => hardLimit.Add(message));
		DictationOrchestrator sut = CreateSut(new AudioBufferingOptions(
			PrerollMs: 0, MaxDurationMs: 100, PostReleaseGraceMs: 0, HardMaxDurationMs: 200));

		sut.Start();
		for (int i = 0; i < 25; i++)
		{
			RaiseFrame(0.5f); // five frames beyond the ceiling: the device tail keeps arriving
		}

		hardLimit.Should().ContainSingle();
		await _mediator.Received(1).Send(Arg.Any<DeliverTranscriptionCommand>(), Arg.Any<CancellationToken>());
		sut.Stage.Should().Be(DictationStage.Idle);
	}

	[Fact]
	public void Cancel_discards_an_in_flight_recording_without_delivering()
	{
		DictationOrchestrator sut = CreateSut();
		bool cancelled = false;
		_stateMachine.Cancelled += (_, _) => cancelled = true;

		sut.Start();
		sut.Cancel();

		sut.Stage.Should().Be(DictationStage.Idle);
		_audio.Received(1).Stop();
		cancelled.Should().BeTrue();
		_mediator.DidNotReceive().Send(Arg.Any<DeliverTranscriptionCommand>(), Arg.Any<CancellationToken>());
	}

	[Fact]
	public void Reassigning_the_hotkey_mid_recording_returns_to_idle_so_the_next_dictation_records()
	{
		// The live hotkey starts a recording, then the binding is reconfigured under it (the
		// user assigns a new hotkey while the old one is still held / armed). The orchestrator must discard
		// the orphaned capture and return to Idle — otherwise it stays stuck Recording, the next start is a
		// no-op, and the overlay never appears for any later dictation.
		DictationOrchestrator sut = CreateSut();
		_activation.Configure(HotkeyBinding.FromKeys(KeyModifiers.None, KeyboardKey.F13), ActivationMode.PushToTalk);
		_activation.HandleKeyDown(KeyboardKey.F13, KeyModifiers.None); // chord satisfied -> recording
		sut.Stage.Should().Be(DictationStage.Recording);

		// Reassign the hotkey while the recording is live.
		_activation.Configure(HotkeyBinding.FromKeys(KeyModifiers.Control, KeyboardKey.J), ActivationMode.PushToTalk);

		sut.Stage.Should().Be(DictationStage.Idle);
		_stateMachine.State.Should().Be(RecordingState.Idle);
		_mediator.DidNotReceive().Send(Arg.Any<DeliverTranscriptionCommand>(), Arg.Any<CancellationToken>());

		// The new hotkey now drives a fresh recording — the pipeline is not wedged.
		_activation.HandleKeyDown(KeyboardKey.Control, KeyModifiers.Control);
		_activation.HandleKeyDown(KeyboardKey.J, KeyModifiers.Control);
		sut.Stage.Should().Be(DictationStage.Recording);
	}

	[Fact]
	public async Task Continuous_mode_auto_restarts_recording_after_a_delivery()
	{
		DictationOrchestrator sut = CreateSut();
		sut.EnableContinuousMode();

		sut.Start();
		await sut.StopAsync(TestContext.Current.CancellationToken);

		sut.Stage.Should().Be(DictationStage.Recording);
		sut.ContinuousMode.Should().BeTrue();
		// Exactly one restart per delivered utterance (initial Start + one auto-restart) — the loop is bounded.
		_audio.Received(2).Start();
	}

	[Fact]
	public async Task A_single_shot_cycle_returns_to_idle_when_continuous_mode_is_off()
	{
		DictationOrchestrator sut = CreateSut();

		sut.Start();
		await sut.StopAsync(TestContext.Current.CancellationToken);

		sut.Stage.Should().Be(DictationStage.Idle);
		_audio.Received(1).Start();
	}

	[Fact]
	public void Esc_exits_continuous_mode_and_returns_to_idle_without_restarting()
	{
		DictationOrchestrator sut = CreateSut();
		sut.EnableContinuousMode();
		sut.Start();

		sut.ExitContinuousMode();

		sut.ContinuousMode.Should().BeFalse();
		sut.Stage.Should().Be(DictationStage.Idle);
		_stateMachine.State.Should().Be(RecordingState.Idle);
	}

	[Fact]
	public void Entering_continuous_mode_is_idempotent()
	{
		DictationOrchestrator sut = CreateSut();

		sut.EnableContinuousMode();
		sut.EnableContinuousMode();

		sut.ContinuousMode.Should().BeTrue();
	}

	[Fact]
	public void A_capture_device_failure_is_logged_and_returns_to_idle()
	{
		DictationOrchestrator sut = CreateSut();
		sut.Start();

		_audio.CaptureFailed += Raise.EventWith(
			new AudioCaptureFailedEventArgs(Domain.Audio.AudioCaptureError.DeviceUnavailable, "device removed"));

		sut.Stage.Should().Be(DictationStage.Idle);
		_stateMachine.State.Should().Be(RecordingState.Idle);
		_logger.Entries.Should().Contain(entry => entry.Level == LogLevel.Error);
	}

	[Fact]
	public async Task Feedback_is_played_at_each_pipeline_transition_when_enabled()
	{
		DictationOrchestrator sut = CreateSut();

		sut.Start();
		await sut.StopAsync(TestContext.Current.CancellationToken);

		_feedback.Received(1).Play(FeedbackSound.RecordingStarted);
		_feedback.Received(1).Play(FeedbackSound.RecordingStopped);
		_feedback.Received(1).Play(FeedbackSound.TranscriptionComplete);
	}

	[Fact]
	public async Task No_feedback_is_played_when_it_is_disabled()
	{
		_feedbackOptions.Enabled = false;
		DictationOrchestrator sut = CreateSut();

		sut.Start();
		await sut.StopAsync(TestContext.Current.CancellationToken);

		_feedback.DidNotReceive().Play(Arg.Any<FeedbackSound>());
	}

	[Fact]
	public async Task A_feedback_failure_does_not_break_dictation()
	{
		_feedback.When(f => f.Play(Arg.Any<FeedbackSound>()))
			.Do(_ => throw new InvalidOperationException("no output device"));
		DictationOrchestrator sut = CreateSut();

		sut.Start();
		await sut.StopAsync(TestContext.Current.CancellationToken);

		// The pipeline still ran to completion despite feedback throwing, and the failure was logged.
		await _mediator.Received(1).Send(Arg.Any<DeliverTranscriptionCommand>(), Arg.Any<CancellationToken>());
		sut.Stage.Should().Be(DictationStage.Idle);
		_logger.Entries.Should().Contain(entry => entry.Level == LogLevel.Error);
	}

	// Raise one capture frame at the given amplitude through the faked audio source, already in the
	// 16 kHz mono clip format so the resampler passes the values through untouched.
	private void RaiseFrame(float amplitude)
	{
		float[] samples = new float[160];
		Array.Fill(samples, amplitude);
		_audio.FrameAvailable += Raise.EventWith(
			new AudioFrameAvailableEventArgs(samples, new CaptureFormat(16_000, 1, 32, AudioSampleFormat.IeeeFloat)));
	}

	// A minimal ILogger that records each entry's level so the error-path tests can assert on it.
	private sealed class CapturingLogger<T> : ILogger<T>
	{
		public List<(LogLevel Level, string Message)> Entries { get; } = [];

		public IDisposable BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;

		public bool IsEnabled(LogLevel logLevel) => true;

		public void Log<TState>(
			LogLevel logLevel,
			EventId eventId,
			TState state,
			Exception? exception,
			Func<TState, Exception?, string> formatter) =>
			Entries.Add((logLevel, formatter(state, exception)));

		private sealed class NullScope : IDisposable
		{
			public static readonly NullScope Instance = new();

			public void Dispose()
			{
			}
		}
	}
}
