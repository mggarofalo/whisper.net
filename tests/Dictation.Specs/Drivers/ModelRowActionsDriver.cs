// Drives the @WHISPER-105 contextual-action scenarios. It builds the REAL ModelViewModel over the REAL
// Mediator pipeline (ListModels / DownloadModel handlers) and the REAL catalog, faking only the
// device-facing cache, downloader, and lifecycle. So it proves the compact list's rule for real: each row
// exposes only the action that fits its state (Download / Cancel / Select), derived from the genuine
// downloaded/active/downloading state the picker computes — not a hand-set flag. The buttons that bind to
// these flags are Presentation glue verified by smoke.

using Application.Ports;
using AwesomeAssertions;
using Domain.Models;
using Logic.AppManagement.Shell;
using Mediator;
using NSubstitute;

namespace Dictation.Specs.Drivers;

public sealed class ModelRowActionsDriver
{
	private readonly ModelViewModel _viewModel;
	private readonly IModelCatalog _catalog;
	private readonly IModelCache _cache;
	private readonly IModelDownloader _downloader;
	private readonly IModelLifecycle _lifecycle;
	private readonly TaskCompletionSource _gate = new();

	public ModelRowActionsDriver(IMediator mediator, IModelCatalog catalog, IModelCache cache, IModelDownloader downloader, IModelLifecycle lifecycle)
	{
		_catalog = catalog;
		_cache = cache;
		_downloader = downloader;
		_lifecycle = lifecycle;

		// Nothing loaded by default, so ListModels can read a non-null status and mark nothing active.
		_lifecycle.Status.Returns(ModelStatus.Unloaded);

		_viewModel = new ModelViewModel(mediator);
	}

	// Load with a chosen model already downloaded and active, and another merely downloaded. Everything
	// else is left un-cached (NSubstitute's default), so it loads as "not downloaded".
	public Task LoadWith(string activeId, string downloadedId)
	{
		MarkCached(activeId);
		MarkCached(downloadedId);
		_lifecycle.Status.Returns(new ModelStatus(activeId, ModelState.Ready));
		return LoadList();
	}

	public Task LoadWithNothingDownloaded() => LoadList();

	// Begin a download on a row but do not await it — the row sits in flight (blocked on the gate) so its
	// downloading state can be asserted.
	public void StartDownload(string id)
	{
		_downloader.DownloadAsync(Arg.Any<WhisperModelCatalogEntry>(), Arg.Any<IProgress<ModelDownloadProgress>>(), Arg.Any<CancellationToken>())
			.Returns(call => new ValueTask<string>(RunGatedDownload(call.ArgAt<CancellationToken>(2), id)));
		_ = Item(id).DownloadCommand.ExecuteAsync(null);
	}

	public void AssertOnlyDownload(string id)
	{
		ModelItemViewModel row = Item(id);
		row.CanDownload.Should().BeTrue("an un-downloaded model offers Download");
		row.IsDownloading.Should().BeFalse();
		row.CanSelect.Should().BeFalse();
	}

	public void AssertOnlySelect(string id)
	{
		ModelItemViewModel row = Item(id);
		row.CanSelect.Should().BeTrue("a downloaded, non-active model offers Select");
		row.CanDownload.Should().BeFalse();
		row.IsDownloading.Should().BeFalse();
	}

	public void AssertOnlyCancel(string id)
	{
		ModelItemViewModel row = Item(id);
		row.IsDownloading.Should().BeTrue("a downloading row offers Cancel");
		row.CanDownload.Should().BeFalse();
		row.CanSelect.Should().BeFalse();
	}

	public void AssertNoActionButActive(string id)
	{
		ModelItemViewModel row = Item(id);
		row.IsActive.Should().BeTrue("the selected model is indicated");
		row.CanDownload.Should().BeFalse();
		row.CanSelect.Should().BeFalse();
		row.IsDownloading.Should().BeFalse();
	}

	private Task LoadList() => _viewModel.LoadCommand.ExecuteAsync(null);

	private void MarkCached(string id) =>
		_cache.IsCached(Arg.Is<WhisperModelCatalogEntry>(e => string.Equals(e.Id, Resolve(id).Id, StringComparison.OrdinalIgnoreCase))).Returns(true);

	private async Task<string> RunGatedDownload(CancellationToken cancellationToken, string id)
	{
		await _gate.Task.WaitAsync(cancellationToken);
		return $"/cache/{id}.bin";
	}

	private ModelItemViewModel Item(string id) =>
		_viewModel.Models.Single(model => string.Equals(model.Id, id, StringComparison.OrdinalIgnoreCase));

	private WhisperModelCatalogEntry Resolve(string id) =>
		_catalog.Find(id) ?? throw new InvalidOperationException($"Unknown model id '{id}'.");
}
