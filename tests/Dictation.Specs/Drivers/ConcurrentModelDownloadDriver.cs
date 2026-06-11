// Drives the @WHISPER-107 concurrent-download scenarios. It builds the REAL ModelViewModel over the REAL
// Mediator pipeline (ListModels / DownloadModel handlers) and the REAL catalog, faking only the
// device-facing downloader — gated PER MODEL ID so several downloads can sit IN FLIGHT at once. That is
// what proves WHISPER-107's outcome: each row owns its own download (progress, IsRunning, Cancel), so
// starting one neither blocks nor disables the others, and cancelling one leaves the rest running. The
// ProgressBar + per-row Cancel button that bind to these are Presentation glue verified by smoke.

using System.Collections.Concurrent;
using Application.Ports;
using AwesomeAssertions;
using Domain.Models;
using Logic.AppManagement.Shell;
using Mediator;
using NSubstitute;

namespace Dictation.Specs.Drivers;

public sealed class ConcurrentModelDownloadDriver
{
	private readonly ModelViewModel _viewModel;
	private readonly ConcurrentDictionary<string, TaskCompletionSource> _gates = new(StringComparer.OrdinalIgnoreCase);
	private readonly ConcurrentDictionary<string, Task> _downloads = new(StringComparer.OrdinalIgnoreCase);

	public ConcurrentModelDownloadDriver(IMediator mediator, IModelDownloader downloader, IModelLifecycle lifecycle)
	{
		// Nothing loaded by default, so ListModels can read a non-null status and mark nothing active.
		lifecycle.Status.Returns(ModelStatus.Unloaded);

		// Every download reports 50% immediately, then blocks on its OWN per-id gate until that row's token
		// cancels (the gates are never opened here — the scenarios only cancel), so two downloads can be
		// observed in flight simultaneously and one can be cancelled without touching the other.
		downloader.DownloadAsync(Arg.Any<WhisperModelCatalogEntry>(), Arg.Any<IProgress<ModelDownloadProgress>>(), Arg.Any<CancellationToken>())
			.Returns(call => new ValueTask<string>(RunGatedDownload(
				call.ArgAt<WhisperModelCatalogEntry>(0),
				call.ArgAt<IProgress<ModelDownloadProgress>>(1),
				call.ArgAt<CancellationToken>(2))));

		_viewModel = new ModelViewModel(mediator);
	}

	public Task LoadList() => _viewModel.LoadCommand.ExecuteAsync(null);

	// Begin a row's download but do not await it — each row owns its own command, so this leaves that row
	// in flight (blocked on its gate) while other rows stay free to start their own downloads.
	public void StartDownload(string id) => _downloads[id] = Item(id).DownloadCommand.ExecuteAsync(null);

	public void Cancel(string id) => Item(id).DownloadCancelCommand.Execute(null);

	public async Task AwaitDownload(string id) => await _downloads[id];

	public void AssertRunningWithProgress(string id)
	{
		ModelItemViewModel row = Item(id);
		row.DownloadCommand.IsRunning.Should().BeTrue($"the '{id}' download is in flight on its own row");
		row.DownloadState.Should().Be(ModelDownloadState.InProgress);
		row.DownloadPercent.Should().Be(50d, "each row reports its own determinate progress");
	}

	public void AssertCancellable(string id) =>
		Item(id).DownloadCancelCommand.CanExecute(null).Should().BeTrue($"the '{id}' row has its own working Cancel");

	public void AssertRowCanStartDownload(string id) =>
		Item(id).DownloadCommand.CanExecute(null).Should().BeTrue($"the '{id}' row stays interactive while another row downloads");

	public void AssertReset(string id)
	{
		ModelItemViewModel row = Item(id);
		row.DownloadState.Should().Be(ModelDownloadState.NotStarted, "a cancelled download resets only its own row");
		row.DownloadPercent.Should().Be(0d);
		row.DownloadCommand.IsRunning.Should().BeFalse();
	}

	private async Task<string> RunGatedDownload(WhisperModelCatalogEntry entry, IProgress<ModelDownloadProgress>? progress, CancellationToken cancellationToken)
	{
		progress?.Report(new ModelDownloadProgress(50, 100));
		TaskCompletionSource gate = _gates.GetOrAdd(entry.Id, _ => new TaskCompletionSource());
		await gate.Task.WaitAsync(cancellationToken);
		return $"/cache/{entry.Id}.bin";
	}

	private ModelItemViewModel Item(string id) =>
		_viewModel.Models.Single(model => string.Equals(model.Id, id, StringComparison.OrdinalIgnoreCase));
}
