// The first-run onboarding flow (WHISPER-51, overhauled in WHISPER-74): it decides whether onboarding is
// needed, then guides the user through model, audio-device, and hotkey setup and an input-permission
// check, and marks setup complete so later launches skip it. It depends on nothing but IMediator and the
// IPermissionProbe port, composing the same Mediator commands the dedicated settings views use
// (ListModels, SwitchActiveModel, DownloadModel, ListCaptureDevices, UpdateSettings, CompleteOnboarding)
// — so it reuses M10's model (WHISPER-27) and audio/hotkey (WHISPER-33) capabilities rather than
// reimplementing them. The model step lists the catalog and downloads the chosen model with LIVE progress
// (no silent Progress: null), the device step lists the real capture devices, and CanComplete gates the
// "Finish" button until setup is usable. A model download happens only when the user explicitly asks for
// it (no automatic egress), and permissions can be re-checked after a denial. Built on
// CommunityToolkit.Mvvm and WPF-free so the behavior is driven for real in specs; the thin onboarding
// window binds to it.

using System.Collections.ObjectModel;
using Application.Audio;
using Application.Models;
using Application.Ports;
using Application.Settings;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Domain.Models;
using Mediator;

namespace Logic.AppManagement.Shell;

public sealed partial class OnboardingViewModel : ObservableValidator
{
	private readonly IMediator _mediator;
	private readonly IPermissionProbe _permissions;

	public OnboardingViewModel(IMediator mediator, IPermissionProbe permissions)
	{
		_mediator = mediator;
		_permissions = permissions;
	}

	/// <summary>The catalog models offered on the model step, one row each.</summary>
	public ObservableCollection<ModelItemViewModel> Models { get; } = [];

	/// <summary>The capture devices offered on the input-device step.</summary>
	public ObservableCollection<AudioDeviceDto> Devices { get; } = [];

	/// <summary>True once the user finishes onboarding; the host closes the flow when this is set.</summary>
	[ObservableProperty]
	private bool _isComplete;

	/// <summary>The id of the model chosen as active, or null until one is chosen.</summary>
	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(CanComplete))]
	private string? _activeModelId;

	/// <summary>The id of the chosen capture device, or null until one is chosen.</summary>
	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(CanComplete))]
	private string? _selectedDeviceId;

	/// <summary>Whether the permission check has been run at least once.</summary>
	[ObservableProperty]
	private bool _permissionsRequested;

	/// <summary>Whether the required input permissions are currently granted.</summary>
	[ObservableProperty]
	private bool _permissionsGranted;

	/// <summary>Setup is usable once a model is active and a capture device is chosen; gates "Finish".</summary>
	public bool CanComplete => ActiveModelId is not null && SelectedDeviceId is not null;

	/// <summary>
	/// Whether onboarding should run: true on a fresh install (setup not yet completed). Read through
	/// Mediator so the host can decide whether to show the flow instead of the normal shell.
	/// </summary>
	public async Task<bool> IsRequiredAsync(CancellationToken cancellationToken = default)
	{
		AppSettingsDto settings = await _mediator.Send(new GetSettingsQuery(), cancellationToken);
		return !settings.SetupCompleted;
	}

	// Load the offered choices: the catalog models (with ratings + download/active state) and the available
	// capture devices, both through the same Mediator queries the settings views use.
	[RelayCommand]
	private async Task LoadChoicesAsync(CancellationToken cancellationToken)
	{
		IReadOnlyList<ModelListItemDto> models = await _mediator.Send(new ListModelsQuery(), cancellationToken);
		Models.Clear();
		foreach (ModelListItemDto model in models)
		{
			Models.Add(new ModelItemViewModel(model));
		}

		ActiveModelId = models.FirstOrDefault(model => model.IsActive)?.Id;

		IReadOnlyList<AudioDeviceDto> devices = await _mediator.Send(new ListCaptureDevicesQuery(), cancellationToken);
		Devices.Clear();
		foreach (AudioDeviceDto device in devices)
		{
			Devices.Add(device);
		}
	}

	// Use a listed model: download it first (with live progress) if needed, then make it active. A failed
	// download leaves the active model unchanged. Mirrors the shell model picker so the behavior is identical.
	[RelayCommand]
	private async Task UseModelAsync(ModelItemViewModel? item, CancellationToken cancellationToken)
	{
		if (item is null)
		{
			return;
		}

		if (!item.IsDownloaded)
		{
			item.DownloadPercent = 0;
			item.DownloadState = ModelDownloadState.InProgress;

			// A synchronous progress sink that updates the row inline as each report arrives; WPF marshals the
			// scalar change to the UI thread and the specs see deterministic, ordered updates.
			IProgress<ModelDownloadProgress> progress = new InlineProgress(report =>
				item.DownloadPercent = report.Percent ?? item.DownloadPercent);

			try
			{
				await _mediator.Send(new DownloadModelCommand(item.Id, progress), cancellationToken);
				item.DownloadPercent = 100;
				item.IsDownloaded = true;
				item.DownloadState = ModelDownloadState.Succeeded;
			}
			catch (Exception) when (cancellationToken.IsCancellationRequested is false)
			{
				item.DownloadState = ModelDownloadState.Failed;
				return;
			}
		}

		await _mediator.Send(new SwitchActiveModelCommand(item.Id), cancellationToken);
		SetActiveModel(item.Id);
	}

	private void SetActiveModel(string modelId)
	{
		ActiveModelId = modelId;
		foreach (ModelItemViewModel model in Models)
		{
			model.IsActive = string.Equals(model.Id, modelId, StringComparison.OrdinalIgnoreCase);
		}
	}

	// Step — model: make the chosen (already-downloaded) model active.
	[RelayCommand]
	private async Task ChooseModelAsync(string modelId, CancellationToken cancellationToken)
	{
		await _mediator.Send(new SwitchActiveModelCommand(modelId), cancellationToken);
		ActiveModelId = modelId;
	}

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
		SelectedDeviceId = deviceId;
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

	// Reports progress synchronously (inline with each Report call), unlike Progress<T> which marshals to a
	// captured context and would race the specs' assertions.
	private sealed class InlineProgress(Action<ModelDownloadProgress> report) : IProgress<ModelDownloadProgress>
	{
		public void Report(ModelDownloadProgress value) => report(value);
	}
}
