// Unit depth for the WHISPER-90 dispatcher seam on the level-overlay view-model, beyond the
// @WHISPER-90 acceptance scenarios: the handlers run against a synchronous TestUiDispatcher with no
// live WPF Application — visibility and per-frame level updates are posted (never a blocking call)
// when off the UI thread, take the CheckAccess fast-path when on it, and Dispose detaches both
// controller subscriptions.

using Application.Ports;
using AwesomeAssertions;
using Domain.Audio;
using NSubstitute;
using Xunit;

namespace Logic.AppManagement.Tests;

public sealed class LevelOverlayViewModelTests
{
	private readonly RecordingStateMachine _stateMachine = new();
	private readonly IAudioSource _audioSource = Substitute.For<IAudioSource>();
	private readonly TestUiDispatcher _dispatcher = new();
	private readonly LevelOverlayController _controller;
	private readonly LevelOverlayViewModel _viewModel;

	public LevelOverlayViewModelTests()
	{
		_controller = new LevelOverlayController(_stateMachine, _audioSource);
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

		_stateMachine.RequestStart();
		EmitFrame(0.5f);

		_dispatcher.PostCount.Should().Be(2, "one visibility change and one level change were marshaled");
		_dispatcher.InvokeAsyncCount.Should().Be(0, "per-frame updates must never block the audio thread");
		_viewModel.IsOverlayVisible.Should().BeTrue();
		_viewModel.Level.Should().BeGreaterThan(0);
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
