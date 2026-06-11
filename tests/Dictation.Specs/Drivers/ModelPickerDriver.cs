// Drives the @WHISPER-27 model picker scenarios. It owns HOW the picker is exercised so the steps stay
// one-liners: it builds the REAL ModelViewModel over the REAL Mediator pipeline (ListModels / Download /
// SwitchActiveModel handlers) and the REAL on-device catalog, faking only the device-facing ports — the
// cache, the downloader, and the model lifecycle. It can therefore prove the list carries ratings, that
// a download reports live progress and then activates, and that a failed download leaves the active
// model unchanged — all without touching disk or the network. The thin WPF view that binds to the
// ViewModel is Presentation glue verified by smoke.

using Application.Ports;
using AwesomeAssertions;
using Domain.Models;
using Domain.Settings;
using Logic.AppManagement.Shell;
using Mediator;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace Dictation.Specs.Drivers;

public sealed class ModelPickerDriver
{
	private readonly ModelViewModel _viewModel;
	private readonly IModelCatalog _catalog;
	private readonly IModelCache _cache;
	private readonly IModelDownloader _downloader;
	private readonly IModelLifecycle _lifecycle;
	private readonly ISettingsStore _store;

	private readonly List<double> _observedProgress = [];
	private string? _targetId;
	private AppSettings _persisted = AppSettings.Default;

	public ModelPickerDriver(IMediator mediator, IModelCatalog catalog, IModelCache cache, IModelDownloader downloader, IModelLifecycle lifecycle, ISettingsStore store)
	{
		_catalog = catalog;
		_cache = cache;
		_downloader = downloader;
		_lifecycle = lifecycle;
		_store = store;

		// Nothing loaded by default, so ListModels can read a non-null status and mark nothing active.
		_lifecycle.Status.Returns(ModelStatus.Unloaded);

		// The settings store round-trips a save into the next load, so switching the active model can be
		// observed as a persisted settings.ModelId — the value the transcriber loads (WHISPER-98).
		_store.LoadAsync(Arg.Any<CancellationToken>()).Returns(_ => _persisted);
		_store.When(s => s.SaveAsync(Arg.Any<AppSettings>(), Arg.Any<CancellationToken>()))
			.Do(call => _persisted = call.Arg<AppSettings>());

		_viewModel = new ModelViewModel(mediator);
	}

	public async Task LoadList() => await _viewModel.LoadCommand.ExecuteAsync(null);

	public async Task GivenDownloadedModelListed(string id)
	{
		WhisperModelCatalogEntry entry = Resolve(id);
		_cache.IsCached(Arg.Is<WhisperModelCatalogEntry>(e => e.Id == entry.Id)).Returns(true);
		ConfigureSwitch(id);
		await LoadList();
	}

	public async Task GivenUndownloadedModelListed(string id)
	{
		_targetId = id;
		// Load first (the row starts un-downloaded), then wire the download to that row's live progress.
		await LoadList();
		ConfigureSuccessfulDownload(id);
		ConfigureSwitch(id);
	}

	public async Task GivenModelWhoseDownloadWillFail(string id)
	{
		_targetId = id;
		_downloader
			.DownloadAsync(Arg.Any<WhisperModelCatalogEntry>(), Arg.Any<IProgress<ModelDownloadProgress>>(), Arg.Any<CancellationToken>())
			.Throws(new InvalidOperationException("download failed"));
		await LoadList();
	}

	// WHISPER-118: the model is the persisted selection but the runtime has not loaded it yet (a fresh
	// launch); the lifecycle stays Unloaded (the ctor default), so ListModels must fall back to settings.
	public void GivenPersistedActiveModelNotYetLoaded(string id) =>
		_persisted = new AppSettings(id, _persisted.Hotkey, _persisted.SilenceThresholdMs, _persisted.FillerWordRemovalEnabled, _persisted.CaptureDeviceId, _persisted.AuditLogEnabled, _persisted.SetupCompleted);

	public async Task SelectModel(string id) => await _viewModel.SelectCommand.ExecuteAsync(Item(id));

	public Task SelectTargetModel() => SelectModel(_targetId!);

	// --- assertions ---

	public void AssertRatingsListed()
	{
		_viewModel.Models.Should().NotBeEmpty();

		// A small model is fast/light/less-accurate; a large one is the reverse — so the ratings are real,
		// derived data, not a constant.
		ModelItemViewModel tiny = Item("tiny");
		tiny.Speed.Should().Be(Application.Models.ModelRating.High);
		tiny.Accuracy.Should().Be(Application.Models.ModelRating.Low);
		tiny.Memory.Should().Be(Application.Models.ModelRating.Low);

		ModelItemViewModel large = Item("large-v3");
		large.Speed.Should().Be(Application.Models.ModelRating.Low);
		large.Accuracy.Should().Be(Application.Models.ModelRating.High);
		large.Memory.Should().Be(Application.Models.ModelRating.High);
	}

	public void AssertSwitchDispatched(string id) =>
		_lifecycle.Received(1).SwitchAsync(id, Arg.Any<CancellationToken>());

	public void AssertViewShowsActive(string id)
	{
		_viewModel.ActiveModelId.Should().Be(id);
		Item(id).IsActive.Should().BeTrue();
	}

	// The selected model was persisted as settings.ModelId — the value WhisperTranscriber loads — so the
	// choice actually drives transcription and survives a restart, not just the in-memory lifecycle status.
	public void AssertActiveModelPersisted(string id) =>
		_persisted.ModelId.Should().Be(id, "switching the active model must persist settings.ModelId");

	public void AssertProgressShown()
	{
		// The row's percent moved with the live reports (an intermediate value was surfaced, not only the
		// final 100), and the download ended in a terminal success.
		_observedProgress.Should().Contain(50d);
		Item(_targetId!).DownloadState.Should().Be(ModelDownloadState.Succeeded);
		Item(_targetId!).DownloadPercent.Should().Be(100d);
	}

	public void AssertTargetBecomesActive() => AssertViewShowsActive(_targetId!);

	public void AssertDownloadFailed()
	{
		Item(_targetId!).DownloadState.Should().Be(ModelDownloadState.Failed);
		Item(_targetId!).IsDownloaded.Should().BeFalse();
	}

	public void AssertTargetNotActivated()
	{
		_viewModel.ActiveModelId.Should().NotBe(_targetId);
		_lifecycle.DidNotReceive().SwitchAsync(_targetId!, Arg.Any<CancellationToken>());
	}

	// --- setup helpers ---

	// A successful download reports live progress (50% then 100%) and returns a cache path. Capturing the
	// row's percent right after the mid-point report proves the progress is surfaced live, not just at the end.
	private void ConfigureSuccessfulDownload(string id)
	{
		ModelItemViewModel row = Item(id);
		_downloader
			.DownloadAsync(Arg.Any<WhisperModelCatalogEntry>(), Arg.Any<IProgress<ModelDownloadProgress>>(), Arg.Any<CancellationToken>())
			.Returns(call =>
			{
				IProgress<ModelDownloadProgress>? progress = call.ArgAt<IProgress<ModelDownloadProgress>>(1);
				progress?.Report(new ModelDownloadProgress(50, 100));
				_observedProgress.Add(row.DownloadPercent);
				progress?.Report(new ModelDownloadProgress(100, 100));
				return $"/cache/{Resolve(id).FileName}";
			});
	}

	// When the lifecycle is switched to the model, its observable status reflects it as the ready, active model.
	private void ConfigureSwitch(string id) =>
		_lifecycle.When(lifecycle => lifecycle.SwitchAsync(id, Arg.Any<CancellationToken>()))
			.Do(_ => _lifecycle.Status.Returns(new ModelStatus(id, ModelState.Ready)));

	private ModelItemViewModel Item(string id) =>
		_viewModel.Models.Single(model => string.Equals(model.Id, id, StringComparison.OrdinalIgnoreCase));

	private WhisperModelCatalogEntry Resolve(string id) =>
		_catalog.Find(id) ?? throw new InvalidOperationException($"Unknown model id '{id}'.");
}
