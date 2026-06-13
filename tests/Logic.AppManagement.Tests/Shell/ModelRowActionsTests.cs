// Inner TDD loop for the model row's contextual actions, WPF-free. The compact list shows
// only the action that fits each row's state — Download (not downloaded), Cancel (downloading), Select
// (downloaded but not active) — instead of a permanent three-button strip. These pin the derived flags
// the view binds (CanDownload / IsDownloading / CanSelect) for every state, and that they re-raise change
// notification when the underlying state changes so the view swaps the visible action live.

using Application.Models;
using AwesomeAssertions;
using Logic.AppManagement.Shell;
using Mediator;
using NSubstitute;
using Xunit;

namespace Logic.AppManagement.Tests.Shell;

public sealed class ModelRowActionsTests
{
	private static ModelItemViewModel Row(bool downloaded, bool active) =>
		new(new ModelListItemDto("base.en", "Base (EN)", 100, default, default, default, IsDownloaded: downloaded, IsActive: active), Substitute.For<IMediator>());

	private static async Task<Unit> WaitForGate(TaskCompletionSource gate, CancellationToken cancellationToken)
	{
		await gate.Task.WaitAsync(cancellationToken);
		return Unit.Value;
	}

	[Fact]
	public void Not_downloaded_offers_only_download()
	{
		ModelItemViewModel row = Row(downloaded: false, active: false);

		row.CanDownload.Should().BeTrue();
		row.IsDownloading.Should().BeFalse();
		row.CanSelect.Should().BeFalse();
	}

	[Fact]
	public void Downloaded_but_not_active_offers_only_select()
	{
		ModelItemViewModel row = Row(downloaded: true, active: false);

		row.CanSelect.Should().BeTrue();
		row.CanDownload.Should().BeFalse();
		row.IsDownloading.Should().BeFalse();
	}

	[Fact]
	public void The_active_model_offers_no_action()
	{
		ModelItemViewModel row = Row(downloaded: true, active: true);

		row.IsActive.Should().BeTrue();
		row.CanSelect.Should().BeFalse("the active model is indicated, not re-selectable");
		row.CanDownload.Should().BeFalse();
		row.IsDownloading.Should().BeFalse();
	}

	[Fact]
	public async Task A_downloading_row_offers_only_cancel_then_only_select_on_success()
	{
		TaskCompletionSource gate = new();
		IMediator mediator = Substitute.For<IMediator>();
		mediator.Send(Arg.Any<DownloadModelCommand>(), Arg.Any<CancellationToken>())
			.Returns(call => new ValueTask<Unit>(WaitForGate(gate, (CancellationToken)call[1])));
		ModelItemViewModel row =
			new(new ModelListItemDto("base.en", "Base (EN)", 100, default, default, default, IsDownloaded: false, IsActive: false), mediator);

		Task download = row.DownloadCommand.ExecuteAsync(null);

		row.IsDownloading.Should().BeTrue();
		row.CanDownload.Should().BeFalse();
		row.CanSelect.Should().BeFalse();

		gate.SetResult();
		await download;

		row.IsDownloading.Should().BeFalse();
		row.CanSelect.Should().BeTrue("a freshly downloaded model can now be selected");
		row.CanDownload.Should().BeFalse();
	}

	[Fact]
	public void Flags_re_raise_change_notification_when_state_changes()
	{
		ModelItemViewModel row = Row(downloaded: false, active: false);
		using var monitor = row.Monitor();

		row.IsDownloaded = true;

		monitor.Should().RaisePropertyChangeFor(x => x.CanDownload);
		monitor.Should().RaisePropertyChangeFor(x => x.CanSelect);
	}

	[Fact]
	public void Becoming_active_hides_its_select_action()
	{
		ModelItemViewModel row = Row(downloaded: true, active: false);
		row.CanSelect.Should().BeTrue();

		row.IsActive = true;

		row.CanSelect.Should().BeFalse();
	}
}
