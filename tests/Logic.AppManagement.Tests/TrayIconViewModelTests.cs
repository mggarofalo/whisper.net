// Unit depth for the dispatcher seam on the tray view-model, beyond the
// acceptance scenarios: the handlers run against a synchronous TestUiDispatcher with no live WPF
// Application — an off-UI-thread status change is posted through the seam, an on-UI-thread change
// takes the CheckAccess fast-path, and Dispose detaches the controller subscription.

using Application.Ports;
using AwesomeAssertions;
using Domain.Recording;
using Logic.AppManagement.Tray;
using Microsoft.Extensions.Hosting;
using NSubstitute;
using Xunit;

namespace Logic.AppManagement.Tests;

public sealed class TrayIconViewModelTests
{
	private readonly RecordingStateMachine _stateMachine = new();
	private readonly TestUiDispatcher _dispatcher = new();
	private readonly TrayController _controller;
	private readonly TrayIconViewModel _viewModel;

	public TrayIconViewModelTests()
	{
		_controller = new TrayController(
			_stateMachine, Substitute.For<IShellPresenter>(), Substitute.For<IHostApplicationLifetime>());
		_viewModel = new TrayIconViewModel(_controller, _dispatcher);
	}

	[Fact]
	public void Posts_status_updates_through_the_dispatcher_when_off_the_ui_thread()
	{
		_dispatcher.IsOnUiThread = false;

		_stateMachine.RequestStart();

		_dispatcher.PostCount.Should().Be(1);
		_viewModel.Status.Should().Be(RecordingState.Recording);
		_viewModel.ToolTipText.Should().Be("Whisper — recording");
	}

	[Fact]
	public void Applies_status_updates_inline_when_already_on_the_ui_thread()
	{
		_dispatcher.IsOnUiThread = true;

		_stateMachine.RequestStart();

		_dispatcher.PostCount.Should().Be(0);
		_dispatcher.InvokeAsyncCount.Should().Be(0);
		_viewModel.Status.Should().Be(RecordingState.Recording);
	}

	[Fact]
	public void Stops_reflecting_status_changes_after_dispose()
	{
		_viewModel.Dispose();

		_stateMachine.RequestStart();

		_viewModel.Status.Should().Be(RecordingState.Idle);
		_dispatcher.PostCount.Should().Be(0);
	}
}
