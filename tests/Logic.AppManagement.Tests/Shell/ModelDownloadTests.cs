// Inner TDD loop for a model row's download command (WHISPER-81; per-row since WHISPER-107), WPF-free.
// These pin the download's command semantics over a gated IMediator: each row exposes IsRunning and
// cannot run concurrently WITH ITSELF (CanExecute is false while that row's download is in flight), a
// Cancel cancels the in-flight token and resets the row without activating anything, and a failure
// surfaces a user-facing DownloadError on the row instead of throwing. The whole command is async (no
// .Result/.Wait), which is what keeps the UI thread free; the ProgressBar + Cancel button that bind to
// these are Presentation glue verified by smoke. Cross-row concurrency is owned by the @WHISPER-107
// acceptance scenarios, which drive the real composition.

using Application.Models;
using AwesomeAssertions;
using Logic.AppManagement.Shell;
using Mediator;
using NSubstitute;
using Xunit;

namespace Logic.AppManagement.Tests.Shell;

public sealed class ModelDownloadTests
{
	private static ModelItemViewModel NewItem(IMediator mediator) =>
		new(new ModelListItemDto("base.en", "Base (EN)", 100, default, default, default, IsDownloaded: false, IsActive: false), mediator);

	private static async Task<Unit> WaitForGate(TaskCompletionSource gate, CancellationToken cancellationToken)
	{
		await gate.Task.WaitAsync(cancellationToken);
		return Unit.Value;
	}

	[Fact]
	public async Task Download_exposes_is_running_and_cannot_run_concurrently()
	{
		TaskCompletionSource gate = new();
		IMediator mediator = Substitute.For<IMediator>();
		mediator.Send(Arg.Any<DownloadModelCommand>(), Arg.Any<CancellationToken>())
			.Returns(call => new ValueTask<Unit>(WaitForGate(gate, (CancellationToken)call[1])));
		ModelItemViewModel item = NewItem(mediator);

		Task download = item.DownloadCommand.ExecuteAsync(null);

		item.DownloadCommand.IsRunning.Should().BeTrue();
		item.DownloadCommand.CanExecute(null).Should().BeFalse("the row cannot download concurrently with itself");
		item.DownloadState.Should().Be(ModelDownloadState.InProgress);

		gate.SetResult();
		await download;

		item.DownloadCommand.IsRunning.Should().BeFalse();
		item.DownloadState.Should().Be(ModelDownloadState.Succeeded);
		item.IsDownloaded.Should().BeTrue();
		item.DownloadPercent.Should().Be(100);
	}

	[Fact]
	public async Task Cancelling_resets_the_row_and_leaves_it_inactive()
	{
		TaskCompletionSource gate = new();
		IMediator mediator = Substitute.For<IMediator>();
		mediator.Send(Arg.Any<DownloadModelCommand>(), Arg.Any<CancellationToken>())
			.Returns(call => new ValueTask<Unit>(WaitForGate(gate, (CancellationToken)call[1])));
		ModelItemViewModel item = NewItem(mediator);

		Task download = item.DownloadCommand.ExecuteAsync(null);
		item.DownloadCommand.IsRunning.Should().BeTrue();

		item.DownloadCancelCommand.Execute(null);
		await download;

		item.DownloadState.Should().Be(ModelDownloadState.NotStarted, "a cancelled download resets the row");
		item.DownloadPercent.Should().Be(0);
		item.IsActive.Should().BeFalse();
		item.IsDownloaded.Should().BeFalse();
	}

	[Fact]
	public async Task A_failed_download_surfaces_a_native_error_and_does_not_activate()
	{
		IMediator mediator = Substitute.For<IMediator>();
		mediator.Send(Arg.Any<DownloadModelCommand>(), Arg.Any<CancellationToken>())
			.Returns<ValueTask<Unit>>(_ => throw new InvalidOperationException("network down"));
		ModelItemViewModel item = NewItem(mediator);

		await item.DownloadCommand.ExecuteAsync(null);

		item.DownloadState.Should().Be(ModelDownloadState.Failed);
		item.DownloadError.Should().NotBeNullOrEmpty("a failed download surfaces a native error, not a crash");
		item.IsActive.Should().BeFalse();
	}
}
