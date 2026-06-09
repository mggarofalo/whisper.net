// Drives the @WHISPER-51 first-run onboarding scenarios and the @WHISPER-74 overhaul. It owns HOW the
// flow is exercised so the steps stay one-liners: it builds the REAL OnboardingViewModel over the REAL
// Mediator pipeline (GetSettings / UpdateSettings / ListModels / SwitchActiveModel / DownloadModel /
// ListCaptureDevices / CompleteOnboarding handlers) and the REAL on-device model catalog, faking only the
// device-facing ports — the settings store (which round-trips a save into the next load, so completing
// onboarding is remembered on the next "launch"), the model cache/downloader/lifecycle, the capture-device
// enumerator, and the permission probe. It can therefore prove first-run shows onboarding, completion is
// persisted, the steps dispatch the right commands, a download only happens on explicit approval and now
// reports LIVE progress, the device/model lists are populated, and Finish is gated until setup is usable —
// all without any OS or network. The thin onboarding window is Presentation glue verified by smoke.

using Application.Ports;
using AwesomeAssertions;
using Dictation.Specs.Support;
using Domain.Audio;
using Domain.Models;
using Domain.Settings;
using Logic.AppManagement.Shell;
using Mediator;
using NSubstitute;

namespace Dictation.Specs.Drivers;

public sealed class OnboardingDriver
{
	private const string ChosenModel = "small.en";
	private const string ChosenDevice = "Mic-B";
	private const string ChosenHotkey = "Ctrl+Shift+D";
	private const string UndownloadedModel = "tiny";

	private readonly OnboardingViewModel _viewModel;
	private readonly ISettingsStore _store;
	private readonly IModelDownloader _downloader;
	private readonly IModelCatalog _catalog;
	private readonly IModelCache _cache;
	private readonly IModelLifecycle _lifecycle;
	private readonly FakeAudioDeviceEnumerator _audioDevices;
	private readonly IPermissionProbe _permissions;

	private readonly List<double> _observedProgress = [];
	private AppSettings _persisted = AppSettings.Default;
	private bool _required;

	public OnboardingDriver(
		IMediator mediator,
		ISettingsStore store,
		IModelDownloader downloader,
		IModelCatalog catalog,
		IModelCache cache,
		IModelLifecycle lifecycle,
		FakeAudioDeviceEnumerator audioDevices,
		IPermissionProbe permissions)
	{
		_store = store;
		_downloader = downloader;
		_catalog = catalog;
		_cache = cache;
		_lifecycle = lifecycle;
		_audioDevices = audioDevices;
		_permissions = permissions;
		_viewModel = new OnboardingViewModel(mediator, permissions);

		// The store round-trips a save into the next load, so completing onboarding is reflected on the
		// next "launch" (AppSettings.Default starts with SetupCompleted = false).
		_store.LoadAsync(Arg.Any<CancellationToken>()).Returns(_ => _persisted);
		_store.When(s => s.SaveAsync(Arg.Any<AppSettings>(), Arg.Any<CancellationToken>()))
			.Do(call => _persisted = call.Arg<AppSettings>());

		// Nothing loaded by default, so ListModels reads a non-null status and marks nothing active.
		_lifecycle.Status.Returns(ModelStatus.Unloaded);
	}

	// --- given ---

	public void NoCompletedSetup() => _persisted = AppSettings.Default;

	public async Task UserCompletedOnboarding() => await _viewModel.CompleteCommand.ExecuteAsync(null);

	public void PermissionsDeniedThenGranted() =>
		_permissions.HasRequiredInputPermissions().Returns(false, true);

	// --- when ---

	public async Task ApplicationStarts() => _required = await _viewModel.IsRequiredAsync();

	public async Task RunGuidedSteps()
	{
		await _viewModel.ChooseModelCommand.ExecuteAsync(ChosenModel);
		await _viewModel.ChooseDeviceCommand.ExecuteAsync(ChosenDevice);
		await _viewModel.ChooseHotkeyCommand.ExecuteAsync(ChosenHotkey);
	}

	public void DeclineOfferedDownload() => _viewModel.DeclineModelDownloadCommand.Execute(null);

	public void RequestPermissionsThenReattempt()
	{
		_viewModel.RequestPermissionsCommand.Execute(null);
		_viewModel.RequestPermissionsCommand.Execute(null);
	}

	// --- @WHISPER-74: load the offered choices (real devices + catalog models) ---

	public async Task LoadChoices()
	{
		_audioDevices.Configure(
			[new AudioDevice("Mic-A", "Microphone A"), new AudioDevice("Mic-B", "Microphone B")],
			defaultId: "Mic-A");
		await _viewModel.LoadChoicesCommand.ExecuteAsync(null);
	}

	public async Task UseUndownloadedModel()
	{
		ModelItemViewModel row = Item(UndownloadedModel);
		ConfigureSuccessfulDownload(row);
		ConfigureSwitch(UndownloadedModel);
		await _viewModel.UseModelCommand.ExecuteAsync(row);
	}

	// --- then ---

	public void AssertOnboardingShown() => _required.Should().BeTrue();

	public void AssertOnboardingNotShown() => _required.Should().BeFalse();

	public void AssertChosenSetupApplied()
	{
		// The model switch went through the lifecycle and the device + hotkey were persisted, all via the
		// Mediator pipeline — so the final persisted settings carry the chosen device and hotkey.
		_persisted.CaptureDeviceId.Should().Be(ChosenDevice);
		_persisted.Hotkey.Chord.Should().Be(HotkeyBinding.Parse(ChosenHotkey).Chord);
	}

	public void AssertNoModelDownloaded() =>
		_downloader.DidNotReceive().DownloadAsync(
			Arg.Any<WhisperModelCatalogEntry>(),
			Arg.Any<IProgress<ModelDownloadProgress>>(),
			Arg.Any<CancellationToken>());

	public void AssertPermissionsGranted()
	{
		_viewModel.PermissionsRequested.Should().BeTrue();
		_viewModel.PermissionsGranted.Should().BeTrue();
	}

	public void AssertDevicesListed()
	{
		_viewModel.Devices.Should().NotBeEmpty();
		_viewModel.Devices.Select(device => device.Id).Should().Contain(["Mic-A", "Mic-B"]);
	}

	public void AssertModelsListed() => _viewModel.Models.Should().NotBeEmpty();

	public void AssertModelDownloadedWithProgressAndActive()
	{
		// An intermediate progress value was surfaced (not just the final 100), the row reached a terminal
		// success, and the downloaded model became the active one.
		_observedProgress.Should().Contain(50d);
		ModelItemViewModel row = Item(UndownloadedModel);
		row.DownloadState.Should().Be(ModelDownloadState.Succeeded);
		row.DownloadPercent.Should().Be(100d);
		_viewModel.ActiveModelId.Should().Be(UndownloadedModel);
	}

	public void AssertCannotCompleteYet() => _viewModel.CanComplete.Should().BeFalse();

	public async Task AssertCanCompleteOnceModelAndDeviceChosen()
	{
		await _viewModel.ChooseModelCommand.ExecuteAsync(ChosenModel);
		await _viewModel.ChooseDeviceCommand.ExecuteAsync(ChosenDevice);
		_viewModel.CanComplete.Should().BeTrue();
	}

	// --- setup helpers (mirrors the @WHISPER-27 model picker driver) ---

	private void ConfigureSuccessfulDownload(ModelItemViewModel row) =>
		_downloader
			.DownloadAsync(Arg.Any<WhisperModelCatalogEntry>(), Arg.Any<IProgress<ModelDownloadProgress>>(), Arg.Any<CancellationToken>())
			.Returns(call =>
			{
				IProgress<ModelDownloadProgress>? progress = call.ArgAt<IProgress<ModelDownloadProgress>>(1);
				progress?.Report(new ModelDownloadProgress(50, 100));
				_observedProgress.Add(row.DownloadPercent);
				progress?.Report(new ModelDownloadProgress(100, 100));
				return $"/cache/{Resolve(row.Id).FileName}";
			});

	private void ConfigureSwitch(string id) =>
		_lifecycle.When(lifecycle => lifecycle.SwitchAsync(id, Arg.Any<CancellationToken>()))
			.Do(_ => _lifecycle.Status.Returns(new ModelStatus(id, ModelState.Ready)));

	private ModelItemViewModel Item(string id) =>
		_viewModel.Models.Single(model => string.Equals(model.Id, id, StringComparison.OrdinalIgnoreCase));

	private WhisperModelCatalogEntry Resolve(string id) =>
		_catalog.Find(id) ?? throw new InvalidOperationException($"Unknown model id '{id}'.");
}
