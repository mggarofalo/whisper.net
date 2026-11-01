// Inner TDD loop for the model row, WPF-free. A row's only actions are its download lifecycle — Download
// (not downloaded) and Cancel (downloading); choosing the model is the list's selection (WHISPER-149). These
// pin the derived flags and the status the view binds for every state, and that they re-raise change
// notification when the underlying state changes so the view updates live.

using Application.Models;
using AwesomeAssertions;
using Logic.AppManagement.Shell;
using Mediator;
using NSubstitute;
using Xunit;

namespace Logic.AppManagement.Tests.Shell;

public sealed class ModelRowActionsTests
{
	private static ModelItemViewModel Row(bool downloaded, bool active, long sizeBytes = 100) =>
		new(new ModelListItemDto("base.en", "Base (EN)", sizeBytes, default, default, default, IsDownloaded: downloaded, IsActive: active), Substitute.For<IMediator>());

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
		row.StatusText.Should().Be("Not downloaded");
	}

	[Fact]
	public void Downloaded_but_not_active_offers_no_action()
	{
		ModelItemViewModel row = Row(downloaded: true, active: false);

		row.CanDownload.Should().BeFalse();
		row.IsDownloading.Should().BeFalse();
		row.StatusText.Should().Be("On this device");
	}

	[Fact]
	public void The_active_model_offers_no_action_and_reads_in_use()
	{
		ModelItemViewModel row = Row(downloaded: true, active: true);

		row.CanDownload.Should().BeFalse();
		row.IsDownloading.Should().BeFalse();
		row.StatusText.Should().Be("In use");
	}

	[Fact]
	public async Task A_downloading_row_offers_only_cancel_then_nothing_on_success()
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
		row.StatusText.Should().Be("Downloading");
		row.IsActivating = true;
		row.StatusText.Should().Be("Downloading, then switching");
		row.IsActivating = false;

		gate.SetResult();
		await download;

		row.IsDownloading.Should().BeFalse();
		row.CanDownload.Should().BeFalse();
		row.StatusText.Should().Be("On this device");
	}

	[Fact]
	public void Flags_and_status_re_raise_change_notification_when_state_changes()
	{
		ModelItemViewModel row = Row(downloaded: false, active: false);
		using var monitor = row.Monitor();

		row.IsDownloaded = true;
		row.IsActivating = true;
		row.IsActive = true;

		monitor.Should().RaisePropertyChangeFor(x => x.CanDownload);
		monitor.Should().RaisePropertyChangeFor(x => x.StatusText);
		monitor.Should().RaisePropertyChangeFor(x => x.Caption);
	}

	[Fact]
	public void Caption_reads_id_size_and_status()
	{
		ModelItemViewModel row = Row(downloaded: true, active: false, sizeBytes: 147_951_465);

		row.Caption.Should().Be("base.en · 141 MB · On this device");
	}

	[Fact]
	public void A_switch_error_is_shown_ahead_of_a_download_error()
	{
		ModelItemViewModel row = Row(downloaded: false, active: false);
		row.DownloadError = "download failed";
		row.ErrorText.Should().Be("download failed");

		row.ActivationError = "switch failed";

		row.ErrorText.Should().Be("switch failed");
	}
}