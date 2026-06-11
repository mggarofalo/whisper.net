// Regression pin for the Select-during-download race (WHISPER-107). Each row owns its download, but the
// section's Select still drives that row's download when the model is not yet cached. CommunityToolkit's
// cancelable AsyncRelayCommand.ExecuteAsync CANCELS the in-flight token and restarts — so Select must NOT
// call ExecuteAsync on a row that is already downloading (the user clicked the row's own Download button,
// then clicked Select on the same row). It must await the in-flight download instead, then activate on
// success — never silently kill the user's download.

using Application.Models;
using AwesomeAssertions;
using Logic.AppManagement.Shell;
using Mediator;
using NSubstitute;
using Xunit;

namespace Logic.AppManagement.Tests.Shell;

public sealed class ModelSelectionConcurrencyTests
{
	private static async Task<Unit> WaitForGate(TaskCompletionSource gate, CancellationToken cancellationToken)
	{
		await gate.Task.WaitAsync(cancellationToken);
		return Unit.Value;
	}

	[Fact]
	public async Task Selecting_a_row_whose_download_is_already_running_awaits_it_without_restarting()
	{
		TaskCompletionSource gate = new();
		int downloadInvocations = 0;
		IMediator mediator = Substitute.For<IMediator>();
		mediator.Send(Arg.Any<ListModelsQuery>(), Arg.Any<CancellationToken>())
			.Returns(new ValueTask<IReadOnlyList<ModelListItemDto>>(
				[new ModelListItemDto("base.en", "Base (EN)", 100, default, default, default, IsDownloaded: false, IsActive: false)]));
		mediator.Send(Arg.Any<DownloadModelCommand>(), Arg.Any<CancellationToken>())
			.Returns(call =>
			{
				downloadInvocations++;
				return new ValueTask<Unit>(WaitForGate(gate, (CancellationToken)call[1]));
			});
		mediator.Send(Arg.Any<SwitchActiveModelCommand>(), Arg.Any<CancellationToken>())
			.Returns(new ValueTask<Unit>(Unit.Value));

		ModelViewModel viewModel = new(mediator);
		await viewModel.LoadCommand.ExecuteAsync(null);
		ModelItemViewModel row = viewModel.Models.Single();

		// The user starts the download from the row, then clicks Select on the SAME row while it runs.
		Task download = row.DownloadCommand.ExecuteAsync(null);
		row.DownloadCommand.IsRunning.Should().BeTrue();
		Task select = viewModel.SelectCommand.ExecuteAsync(row);

		// Select must not have cancelled or restarted the in-flight download.
		row.DownloadCommand.IsRunning.Should().BeTrue("Select awaits the running download, it does not restart it");
		downloadInvocations.Should().Be(1, "the row's download was not re-triggered by Select");

		gate.SetResult();
		await download;
		await select;

		// The single download completed and the model was then activated.
		downloadInvocations.Should().Be(1);
		viewModel.ActiveModelId.Should().Be("base.en");
		row.IsActive.Should().BeTrue();
	}
}
