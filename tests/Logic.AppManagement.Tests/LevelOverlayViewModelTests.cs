// Unit depth for the dispatcher seam on the level-overlay view-model, beyond the
// acceptance scenarios: the handlers run against a synchronous TestUiDispatcher with no
// live WPF Application — visibility and per-frame level updates are posted (never a blocking call)
// when off the UI thread, take the CheckAccess fast-path when on it, and Dispose detaches both
// controller subscriptions.

using Application.Dictation;
using Application.Models;
using Application.Ports;
using AwesomeAssertions;
using CommunityToolkit.Mvvm.Messaging;
using Domain.Audio;
using Logic.AppManagement.Tests.Support;
using NSubstitute;
using Xunit;

namespace Logic.AppManagement.Tests;

public sealed class LevelOverlayViewModelTests
{
	private readonly RecordingStateMachine _stateMachine = new();
	private readonly IAudioSource _audioSource = Substitute.For<IAudioSource>();
	private readonly IMessenger _messenger = new WeakReferenceMessenger();
	private readonly ManualTimeProvider _time = new();
	private readonly TestUiDispatcher _dispatcher = new();
	private readonly LevelOverlayController _controller;
	private readonly LevelOverlayViewModel _viewModel;

	public LevelOverlayViewModelTests()
	{
		_controller = new LevelOverlayController(_stateMachine, _audioSource, _messenger, _time);
		_viewModel = new LevelOverlayViewModel(_controller, _dispatcher);
	}

	private void EmitFrame(float amplitude)
	{
		float[] samples = new float[480];
		Array.Fill(samples, amplitude);
		_audioSource.FrameAvailable += Raise.EventWith(
			new AudioFrameAvailableEventArgs(samples, new CaptureFormat(16_000, 1, 32, AudioSampleFormat.IeeeFloat)));
	}

	[Fact]
	public void Posts_visibility_and_level_updates_when_off_the_ui_thread()
	{
		_dispatcher.IsOnUiThread = false;
		bool visibleWhenPresentArrived = false;
		_viewModel.PresentRequested += (_, _) => visibleWhenPresentArrived = _viewModel.IsOverlayVisible;

		_stateMachine.RequestStart();
		EmitFrame(0.5f);

		// Starting recording marshals visibility, state, the initial elapsed update and the present request;
		// the frame marshals the level — five Posts, never a blocking InvokeAsync. The present request is
		// posted after the visibility update, so the window is already shown when it re-presents itself.
		_dispatcher.PostCount.Should().Be(5);
		visibleWhenPresentArrived.Should().BeTrue();
		_dispatcher.InvokeAsyncCount.Should().Be(0, "per-frame updates must never block the audio thread");
		_viewModel.IsOverlayVisible.Should().BeTrue();
		_viewModel.State.Should().Be(OverlayState.Recording);
		_viewModel.Level.Should().BeGreaterThan(0);
	}

	[Fact]
	public void A_message_takes_the_meter_and_elapsed_slots_in_the_single_row()
	{
		_dispatcher.IsOnUiThread = true;

		_stateMachine.RequestStart();
		_viewModel.ShowsLevel.Should().BeTrue();
		_viewModel.ShowsElapsed.Should().BeTrue();

		_stateMachine.RequestStop();
		_viewModel.ShowsLevel.Should().BeTrue("transcribing keeps the (quiet) meter");
		_viewModel.ShowsElapsed.Should().BeFalse("elapsed time is a recording cue");

		_messenger.Send(new DictationNoAudioMessage());
		_viewModel.Message.Should().Be(LevelOverlayController.NoAudioMessage);
		_viewModel.ShowsLevel.Should().BeFalse("the message sits in the meter's place");
		_viewModel.ShowsElapsed.Should().BeFalse();
	}

	[Fact]
	public void Warming_shows_neither_meter_nor_elapsed()
	{
		_dispatcher.IsOnUiThread = true;

		_messenger.Send(new ModelWarmupChangedMessage(true));

		_viewModel.State.Should().Be(OverlayState.Warming);
		_viewModel.ShowsLevel.Should().BeFalse();
		_viewModel.ShowsElapsed.Should().BeFalse();
		_viewModel.Message.Should().BeNull();
	}

	[Fact]
	public void Applies_updates_inline_when_already_on_the_ui_thread()
	{
		_dispatcher.IsOnUiThread = true;

		_stateMachine.RequestStart();
		EmitFrame(0.5f);

		_dispatcher.PostCount.Should().Be(0);
		_viewModel.IsOverlayVisible.Should().BeTrue();
		_viewModel.Level.Should().BeGreaterThan(0);
	}

	[Fact]
	public void Stops_reflecting_controller_changes_after_dispose()
	{
		_viewModel.Dispose();

		_stateMachine.RequestStart();
		EmitFrame(0.5f);

		_viewModel.IsOverlayVisible.Should().BeFalse();
		_viewModel.Level.Should().Be(0);
		_dispatcher.PostCount.Should().Be(0);
	}
}
