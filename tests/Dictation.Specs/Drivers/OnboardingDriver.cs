// Drives the @WHISPER-51 first-run onboarding scenarios. It owns HOW the flow is exercised so the steps
// stay one-liners: it builds the REAL OnboardingViewModel over the REAL Mediator pipeline (GetSettings /
// UpdateSettings / SwitchActiveModel / DownloadModel / CompleteOnboarding handlers) and faked ports —
// the settings store (which round-trips a save into the next load, so completing onboarding is
// remembered on the next "launch"), the model lifecycle + downloader, and the permission probe. It can
// therefore prove first-run shows onboarding, completion is persisted, the steps dispatch the right
// commands, a download only happens on explicit approval, and permissions can be re-attempted — without
// any OS or network. The thin onboarding window is Presentation glue verified by smoke.

using Application.Ports;
using AwesomeAssertions;
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

	private readonly OnboardingViewModel _viewModel;
	private readonly ISettingsStore _store;
	private readonly IModelDownloader _downloader;
	private readonly IPermissionProbe _permissions;

	private AppSettings _persisted = AppSettings.Default;
	private bool _required;

	public OnboardingDriver(IMediator mediator, ISettingsStore store, IModelDownloader downloader, IPermissionProbe permissions)
	{
		_store = store;
		_downloader = downloader;
		_permissions = permissions;
		_viewModel = new OnboardingViewModel(mediator, permissions);

		// The store round-trips a save into the next load, so completing onboarding is reflected on the
		// next "launch" (AppSettings.Default starts with SetupCompleted = false).
		_store.LoadAsync(Arg.Any<CancellationToken>()).Returns(_ => _persisted);
		_store.When(s => s.SaveAsync(Arg.Any<AppSettings>(), Arg.Any<CancellationToken>()))
			.Do(call => _persisted = call.Arg<AppSettings>());
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
			Arg.Any<Domain.Models.WhisperModelCatalogEntry>(),
			Arg.Any<IProgress<Domain.Models.ModelDownloadProgress>>(),
			Arg.Any<CancellationToken>());

	public void AssertPermissionsGranted()
	{
		_viewModel.PermissionsRequested.Should().BeTrue();
		_viewModel.PermissionsGranted.Should().BeTrue();
	}
}
