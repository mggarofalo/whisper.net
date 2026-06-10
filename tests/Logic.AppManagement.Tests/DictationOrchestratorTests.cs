// Unit depth for the WHISPER-14 dictation orchestrator, beyond the @WHISPER-14 acceptance scenarios.
// Pins down the explicit stage path (Idle -> Recording -> Transcribing -> Delivering -> Idle) and its
// observability, the hotkey start signal, the concurrency guard against a second capture, the Esc
// cancel that discards a capture, and the two error paths (a failed delivery and a device capture
// failure) that must log and return the pipeline to a safe Idle. The history write-through (WHISPER-110)
// is pinned here too: a delivered result dispatches a RecordTranscriptionCommand, an undelivered one
// does not, and a failed history write is swallowed with a warning. Every port is an NSubstitute fake,
// so the orchestration is exercised with no real audio, model, or delivery.

using Application.Configuration;
using Application.History;
using Application.Ports;
using Application.Transcription;
using AwesomeAssertions;
using Domain.Feedback;
using Domain.Input;
using Domain.Recording;
using Domain.Settings;
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
	private readonly AudioFeedbackOptions _feedbackOptions = new();
	private readonly CapturingLogger<DictationOrchestrator> _logger = new();

	public DictationOrchestratorTests() =>
		// Default the delivery pipeline to a successful result so the non-error tests don't hit the catch.
		_mediator
			.Send(Arg.Any<DeliverTranscriptionCommand>(), Arg.Any<CancellationToken>())
			.Returns(new DeliveryResult(Delivered: true, Text: "the result"));

	private DictationOrchestrator CreateSut() =>
		new(_audio, _stateMachine, _activation, new AudioResampler(), new AudioBufferingOptions(), _mediator,
			_feedback, Options.Create(_feedbackOptions), Substitute.For<IUserNotifier>(), _logger);

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
