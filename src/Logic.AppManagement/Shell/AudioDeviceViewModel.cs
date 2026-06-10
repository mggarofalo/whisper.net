// The shell's audio device section (WHISPER-33; ComboBox + missing-device handling in WHISPER-80): lists
// the available capture devices and the current selection, and persists a change. It depends on nothing but
// IMediator — it loads via ListCaptureDevicesQuery + GetSettingsQuery and saves via UpdateSettingsCommand,
// carrying the whole settings DTO with the device swapped so the rest of the user's settings are preserved.
// A persisted device that is no longer present does not crash or silently blank the picker: the view-model
// falls back to "follow system default" for the live selection and surfaces a clear warning, while leaving
// the persisted id untouched so the device is restored if it returns. The selection survives a reload
// because the update is persisted. Built on CommunityToolkit.Mvvm and WPF-free so the behavior is driven for
// real in specs; the thin ComboBox view binds to it.

using System.Collections.ObjectModel;
using Application.Audio;
using Application.Settings;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Domain.Audio;
using Mediator;

namespace Logic.AppManagement.Shell;

public sealed partial class AudioDeviceViewModel : ObservableValidator, IFeatureViewModel
{
	private readonly IMediator _mediator;

	private AppSettingsDto? _settings;

	public AudioDeviceViewModel(IMediator mediator) => _mediator = mediator;

	/// <summary>The available capture devices, plus the "follow system default" choice.</summary>
	public ObservableCollection<AudioDeviceDto> Devices { get; } = [];

	/// <summary>The id of the currently selected capture device (or AudioDevice.SystemDefault).</summary>
	[ObservableProperty]
	private string? _selectedDeviceId;

	/// <summary>Set when the persisted device is no longer present: a clear fallback message for the view,
	/// null when the selected device is available. Surfacing this (instead of a blank picker) is the
	/// device-removed handling of WHISPER-80.</summary>
	[ObservableProperty]
	private string? _unavailableDeviceWarning;

	/// <summary>True while a load is in flight, suppressing commit-on-selection during the programmatic
	/// selection a reload performs (a user pick outside a load is a genuine commit).</summary>
	[ObservableProperty]
	private bool _isLoading;

	[ObservableProperty]
	private bool _isActive;

	/// <summary>The device id currently persisted in settings — what a selection change is compared
	/// against to tell a real user pick from the programmatic selection a reload performs.</summary>
	public string? CommittedDeviceId => _settings?.CaptureDeviceId;

	// The commit decision that used to live in the view's SelectionChanged code-behind (WHISPER-92):
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

	public void OnNavigatedTo() => IsActive = true;

	public void OnNavigatedFrom() => IsActive = false;

	// Load the device list and the persisted selection through Mediator. If the persisted device is gone,
	// fall back to system default for the live selection and warn, without overwriting the persisted id.
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

			string persisted = _settings.CaptureDeviceId;
			bool present = persisted == AudioDevice.SystemDefault || Devices.Any(device => device.Id == persisted);

			if (present)
			{
				SelectedDeviceId = persisted;
				UnavailableDeviceWarning = null;
			}
			else
			{
				// The saved microphone is unplugged/removed: keep recording usable by following the system
				// default, and tell the user clearly rather than showing an empty selection.
				SelectedDeviceId = AudioDevice.SystemDefault;
				UnavailableDeviceWarning =
					$"The saved microphone ('{persisted}') is no longer available; using the system default.";
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

		await _mediator.Send(new UpdateSettingsCommand(_settings with { CaptureDeviceId = deviceId }), cancellationToken);
		_settings = _settings with { CaptureDeviceId = deviceId };
		OnPropertyChanged(nameof(CommittedDeviceId));
		SelectedDeviceId = deviceId;
		UnavailableDeviceWarning = null;
	}
}
