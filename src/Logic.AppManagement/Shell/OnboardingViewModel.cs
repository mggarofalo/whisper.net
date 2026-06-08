// The first-run onboarding flow (WHISPER-51): it decides whether onboarding is needed, guides the user
// through model, audio-device, and hotkey setup, checks input permissions, and marks setup complete so
// later launches skip it. It depends on nothing but IMediator and the IPermissionProbe port, composing
// the same Mediator commands the dedicated settings views use (SwitchActiveModel, DownloadModel,
// UpdateSettings) plus CompleteOnboarding — so it reuses M10's model (WHISPER-27) and audio/hotkey
// (WHISPER-33) capabilities rather than reimplementing them. A model download happens only when the user
// explicitly approves it (no automatic egress), and permissions can be re-checked after a denial. Built
// on CommunityToolkit.Mvvm and WPF-free so the behavior is driven for real in specs; the thin onboarding
// window binds to it.

using Application.Models;
using Application.Ports;
using Application.Settings;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Mediator;

namespace Logic.AppManagement.Shell;

public sealed partial class OnboardingViewModel : ObservableObject
{
	private readonly IMediator _mediator;
	private readonly IPermissionProbe _permissions;

	public OnboardingViewModel(IMediator mediator, IPermissionProbe permissions)
	{
		_mediator = mediator;
		_permissions = permissions;
	}

	/// <summary>True once the user finishes onboarding; the host closes the flow when this is set.</summary>
	[ObservableProperty]
	private bool _isComplete;

	/// <summary>Whether the permission check has been run at least once.</summary>
	[ObservableProperty]
	private bool _permissionsRequested;

	/// <summary>Whether the required input permissions are currently granted.</summary>
	[ObservableProperty]
	private bool _permissionsGranted;

	/// <summary>
	/// Whether onboarding should run: true on a fresh install (setup not yet completed). Read through
	/// Mediator so the host can decide whether to show the flow instead of the normal shell.
	/// </summary>
	public async Task<bool> IsRequiredAsync(CancellationToken cancellationToken = default)
	{
		AppSettingsDto settings = await _mediator.Send(new GetSettingsQuery(), cancellationToken);
		return !settings.SetupCompleted;
	}

	// Step — model: make the chosen (already-downloaded) model active.
	[RelayCommand]
	private async Task ChooseModelAsync(string modelId, CancellationToken cancellationToken) =>
		await _mediator.Send(new SwitchActiveModelCommand(modelId), cancellationToken);

	// Step — model download: only ever runs when the user explicitly approves it (no automatic egress).
	[RelayCommand]
	private async Task ApproveModelDownloadAsync(string modelId, CancellationToken cancellationToken) =>
		await _mediator.Send(new DownloadModelCommand(modelId, Progress: null), cancellationToken);

	// Declining the offered download does nothing — in particular it triggers no network egress.
	[RelayCommand]
	private void DeclineModelDownload()
	{
	}

	// Step — audio device: persist the chosen capture device, preserving the rest of the settings.
	[RelayCommand]
	private async Task ChooseDeviceAsync(string deviceId, CancellationToken cancellationToken)
	{
		AppSettingsDto settings = await _mediator.Send(new GetSettingsQuery(), cancellationToken);
		await _mediator.Send(new UpdateSettingsCommand(settings with { CaptureDeviceId = deviceId }), cancellationToken);
	}

	// Step — hotkey: persist the chosen dictation hotkey, preserving the rest of the settings.
	[RelayCommand]
	private async Task ChooseHotkeyAsync(string chord, CancellationToken cancellationToken)
	{
		AppSettingsDto settings = await _mediator.Send(new GetSettingsQuery(), cancellationToken);
		await _mediator.Send(new UpdateSettingsCommand(settings with { Hotkey = chord }), cancellationToken);
	}

	// Step — permissions: check the OS input permissions. Re-runnable so the user can re-attempt after
	// granting them, without restarting onboarding.
	[RelayCommand]
	private void RequestPermissions()
	{
		PermissionsRequested = true;
		PermissionsGranted = _permissions.HasRequiredInputPermissions();
	}

	// Finish: persist that setup is complete, so subsequent launches skip onboarding.
	[RelayCommand]
	private async Task CompleteAsync(CancellationToken cancellationToken)
	{
		await _mediator.Send(new CompleteOnboardingCommand(), cancellationToken);
		IsComplete = true;
	}
}
