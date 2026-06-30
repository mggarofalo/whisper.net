// The shell's audio device section: lists the available capture devices and the current selection,
// and persists a change. It depends on IMediator (load via ListCaptureDevicesQuery + GetSettingsQuery,
// save via UpdateSettingsCommand, carrying the whole settings DTO with the device swapped) and the
// shared DeviceSelectionPolicy, which resolves the saved selection against what's present — by id, and
// then by friendly NAME when the endpoint id has changed across reboots (the common USB/Bluetooth/dock
// case). On a name match the stored id is healed so the warning never returns and capture pins the
// device. A genuinely-missing device falls back to "follow system default" for the live selection and
// surfaces a clear warning (naming the device), leaving the persisted selection intact so it is
// restored if the device returns. Built on CommunityToolkit.Mvvm and WPF-free so the behavior is driven
// for real in specs; the thin ComboBox view binds to it.

using System.Collections.ObjectModel;
using Application.Audio;
using Application.Settings;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Domain.Audio;
using Logic.AudioManagement;
using Mediator;

namespace Logic.AppManagement.Shell;

public sealed partial class AudioDeviceViewModel : FeatureViewModel
{
	private readonly IMediator _mediator;
	private readonly DeviceSelectionPolicy _selectionPolicy;

	private AppSettingsDto? _settings;

	public AudioDeviceViewModel(IMediator mediator, DeviceSelectionPolicy selectionPolicy)
	{
		_mediator = mediator;
		_selectionPolicy = selectionPolicy;
	}

	/// <summary>The available capture devices, plus the "follow system default" choice.</summary>
	public ObservableCollection<AudioDeviceDto> Devices { get; } = [];

	/// <summary>The id of the currently selected capture device (or AudioDevice.SystemDefault).</summary>
	[ObservableProperty]
	private string? _selectedDeviceId;

	/// <summary>Set when the persisted device is no longer present: a clear fallback message for the view,
	/// null when the selected device is available. Surfacing this instead of a blank picker handles
	/// the device-removed case.</summary>
	[ObservableProperty]
	private string? _unavailableDeviceWarning;

	/// <summary>True while a load is in flight, suppressing commit-on-selection during the programmatic
	/// selection a reload performs (a user pick outside a load is a genuine commit).</summary>
	[ObservableProperty]
	private bool _isLoading;

	/// <summary>The device id currently persisted in settings — what a selection change is compared
	/// against to tell a real user pick from the programmatic selection a reload performs.</summary>
	public string? CommittedDeviceId => _settings?.CaptureDeviceId;

	// Auto-load the device list on first activation: the picker opens populated, the cached
	// instance does not re-query on later tab switches, and Refresh stays the manual re-query.
	protected override IAsyncRelayCommand FirstActivationLoadCommand => LoadCommand;

	// The commit decision that used to live in the view's SelectionChanged code-behind:
	// the ComboBox two-way binds SelectedDeviceId, and a change commits only when it is a genuine user
	// pick — never while a load repopulates the picker, and only when the choice differs from what is
	// already persisted (so the missing-device fallback to system default is never written back).
	partial void OnSelectedDeviceIdChanged(string? value)
	{
		if (!IsLoading && value is not null && value != CommittedDeviceId)
		{
			SelectCommand.Execute(value);
		}
	}

	// Load the device list and the persisted selection through Mediator, then resolve the selection
	// against what's present via the shared policy: present by id -> reflect it; recovered by name after a
	// changed id -> reflect it and heal the stored id; genuinely gone -> follow the system default and warn.
	[RelayCommand]
	private async Task LoadAsync(CancellationToken cancellationToken)
	{
		IsLoading = true;
		try
		{
			IReadOnlyList<AudioDeviceDto> devices = await _mediator.Send(new ListCaptureDevicesQuery(), cancellationToken);
			_settings = await _mediator.Send(new GetSettingsQuery(), cancellationToken);
			OnPropertyChanged(nameof(CommittedDeviceId));

			Devices.Clear();

			// Always offer "follow the OS default" as the first choice.
			Devices.Add(new AudioDeviceDto(AudioDevice.SystemDefault, "System default", IsSystemDefault: false));
			foreach (AudioDeviceDto device in devices)
			{
				Devices.Add(device);
			}

			IReadOnlyList<AudioDevice> available = [.. devices.Select(device => new AudioDevice(device.Id, device.Name))];
			string? systemDefaultId = devices.FirstOrDefault(device => device.IsSystemDefault)?.Id;
			DeviceResolution resolution = _selectionPolicy.Resolve(
				_settings.CaptureDeviceId, _settings.CaptureDeviceName, available, systemDefaultId);

			if (resolution.FollowsDefault)
			{
				// Following the OS default (the sentinel selection): no specific device, no warning.
				SelectedDeviceId = AudioDevice.SystemDefault;
				UnavailableDeviceWarning = null;
			}
			else if (resolution.Substituted || resolution.DeviceId is null)
			{
				// The saved microphone is unplugged/removed and no same-named device is present: keep
				// recording usable by following the system default, and tell the user clearly (naming the
				// device when we recorded its name) rather than showing an empty selection.
				SelectedDeviceId = AudioDevice.SystemDefault;
				UnavailableDeviceWarning = string.IsNullOrWhiteSpace(_settings.CaptureDeviceName)
					? "The saved microphone is no longer available; using the system default."
					: $"The saved microphone \"{_settings.CaptureDeviceName}\" is no longer available; using the system default.";
			}
			else
			{
				// Present by id, or recovered by friendly name after the endpoint id changed. Reflect the
				// resolved device; if the id moved (a name match), heal the stored selection so the warning
				// never returns and capture pins the device by its current id.
				SelectedDeviceId = resolution.DeviceId;
				UnavailableDeviceWarning = null;

				if (resolution.DeviceId != _settings.CaptureDeviceId)
				{
					string? name = Devices.FirstOrDefault(device => device.Id == resolution.DeviceId)?.Name;
					await PersistSelectionAsync(resolution.DeviceId, name, cancellationToken);
				}
			}
		}
		finally
		{
			IsLoading = false;
		}
	}

	// Persist a new device selection by submitting the whole settings with the device swapped. Clears any
	// "device unavailable" warning, since the user has now chosen an available device.
	[RelayCommand]
	private async Task SelectAsync(string? deviceId, CancellationToken cancellationToken)
	{
		if (deviceId is null || _settings is null)
		{
			return;
		}

		// Record the friendly name alongside the id so the selection can self-heal if the id later changes.
		// "Follow system default" has no specific device, so it carries no name.
		string? name = deviceId == AudioDevice.SystemDefault
			? null
			: Devices.FirstOrDefault(device => device.Id == deviceId)?.Name;

		await PersistSelectionAsync(deviceId, name, cancellationToken);
		SelectedDeviceId = deviceId;
		UnavailableDeviceWarning = null;
	}

	// Persist the id+name pair through the settings pipeline and keep the local snapshot in step so the
	// CommittedDeviceId comparison (which gates commit-on-selection) reflects what was just saved.
	private async Task PersistSelectionAsync(string deviceId, string? deviceName, CancellationToken cancellationToken)
	{
		AppSettingsDto updated = _settings! with { CaptureDeviceId = deviceId, CaptureDeviceName = deviceName };
		await _mediator.Send(new UpdateSettingsCommand(updated), cancellationToken);
		_settings = updated;
		OnPropertyChanged(nameof(CommittedDeviceId));
	}
}
