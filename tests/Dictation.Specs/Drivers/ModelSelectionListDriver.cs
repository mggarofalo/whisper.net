// Drives the selection-list scenarios. It builds the REAL ModelViewModel over the REAL Mediator pipeline
// (ListModels / DownloadModel / SwitchActiveModel handlers) and the REAL catalog, faking only the
// device-facing cache, downloader, lifecycle and settings store. "Choosing a row" is exactly what the
// ListBox glue does: it runs SelectCommand with that row and leaves the view-model to decide when the
// selection mark (ActiveModel) moves. The ListBox itself is Presentation glue verified by smoke.

using System.Text.RegularExpressions;
using Application.Ports;
using AwesomeAssertions;
using Domain.Models;
using Domain.Settings;
using Logic.AppManagement.Shell;
using Mediator;
using NSubstitute;

namespace Dictation.Specs.Drivers;

public sealed partial class ModelSelectionListDriver
{
	private readonly ModelViewModel _viewModel;
	private readonly IModelCatalog _catalog;
	private readonly IModelCache _cache;
	private readonly IModelDownloader _downloader;
	private readonly IModelLifecycle _lifecycle;
	private readonly TaskCompletionSource _gate = new(TaskCreationOptions.RunContinuationsAsynchronously);
	private readonly List<Task> _inFlight = [];
	private AppSettings _persisted = AppSettings.Default;

	public ModelSelectionListDriver(IMediator mediator, IModelCatalog catalog, IModelCache cache, IModelDownloader downloader, IModelLifecycle lifecycle, ISettingsStore store)
	{
		_catalog = catalog;
		_cache = cache;
		_downloader = downloader;
		_lifecycle = lifecycle;

		_lifecycle.Status.Returns(ModelStatus.Unloaded);
		store.LoadAsync(Arg.Any<CancellationToken>()).Returns(_ => _persisted);
		store.When(s => s.SaveAsync(Arg.Any<AppSettings>(), Arg.Any<CancellationToken>()))
			.Do(call => _persisted = call.Arg<AppSettings>());

		// Every download waits on the held gate unless a scenario releases it, so a choice can be observed
		// mid-download; downloads started after the gate opens complete at once.
		_downloader.DownloadAsync(Arg.Any<WhisperModelCatalogEntry>(), Arg.Any<IProgress<ModelDownloadProgress>>(), Arg.Any<CancellationToken>())
			.Returns(call => new ValueTask<string>(HeldDownload(call.ArgAt<WhisperModelCatalogEntry>(0), call.ArgAt<CancellationToken>(2))));

		_viewModel = new ModelViewModel(mediator);
	}

	public Task LoadWith(string activeId, string downloadedId)
	{
		MarkCached(activeId);
		MarkCached(downloadedId);
		_lifecycle.Status.Returns(new ModelStatus(activeId, ModelState.Ready));
		return _viewModel.LoadCommand.ExecuteAsync(null);
	}

	public void SwitchWillFail(string id) =>
		_lifecycle.SwitchAsync(id, Arg.Any<CancellationToken>()).Returns(_ => ValueTask.FromException(new InvalidOperationException("load failed")));

	public Task Choose(string id) => _viewModel.SelectCommand.ExecuteAsync(Item(id));

	// Choose a row whose download is held at the gate: the choice stays pending until ReleaseDownload.
	public void ChooseWhileDownloadHeld(string id) => _inFlight.Add(_viewModel.SelectCommand.ExecuteAsync(Item(id)));

	public async Task ReleaseDownload()
	{
		_gate.TrySetResult();
		await Task.WhenAll(_inFlight);
	}

	public async Task DownloadFromRow(string id)
	{
		_gate.TrySetResult();
		await Item(id).DownloadCommand.ExecuteAsync(null);
	}

	// --- assertions ---

	public void AssertStatus(string id, string status) =>
		Item(id).StatusText.Should().Be(status);

	public void AssertCaptionsDescribeEachRow()
	{
		foreach (ModelItemViewModel row in _viewModel.Models)
		{
			row.Caption.Should().Be($"{row.Id} · {row.SizeText} · {row.StatusText}");
			row.SizeText.Should().MatchRegex(SizePattern(), $"{row.Id}'s download size reads as MB or GB");
		}
	}

	public void AssertInUse(string id)
	{
		_viewModel.ActiveModel.Should().BeSameAs(Item(id), "the list's selection is the model in use");
		_viewModel.ActiveModelId.Should().Be(id);
		_viewModel.Models.Where(m => m.IsActive).Should().ContainSingle().Which.Id.Should().Be(id);
		Item(id).StatusText.Should().Be("In use");
	}

	public void AssertSwitchFailureExplained(string id) =>
		Item(id).ErrorText.Should().Contain("Couldn't switch");

	public void AssertNoSwitchRequested() =>
		_lifecycle.DidNotReceive().SwitchAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());

	// --- helpers ---

	private async Task<string> HeldDownload(WhisperModelCatalogEntry entry, CancellationToken cancellationToken)
	{
		await _gate.Task.WaitAsync(cancellationToken);
		return $"/cache/{entry.FileName}";
	}

	private void MarkCached(string id) =>
		_cache.IsCached(Arg.Is<WhisperModelCatalogEntry>(e => string.Equals(e.Id, Resolve(id).Id, StringComparison.OrdinalIgnoreCase))).Returns(true);

	private ModelItemViewModel Item(string id) =>
		_viewModel.Models.Single(model => string.Equals(model.Id, id, StringComparison.OrdinalIgnoreCase));

	private WhisperModelCatalogEntry Resolve(string id) =>
		_catalog.Find(id) ?? throw new InvalidOperationException($"Unknown model id '{id}'.");

	[GeneratedRegex(@"^\d+(\.\d)? (MB|GB)$")]
	private static partial Regex SizePattern();
}
