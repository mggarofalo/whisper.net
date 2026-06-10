// The shell's audio device section (WHISPER-33): lists the available capture devices and the current
// selection, and persists a change. It depends on nothing but IMediator — it loads via
// ListCaptureDevicesQuery + GetSettingsQuery and saves via UpdateSettingsCommand, carrying the whole
// settings DTO with the device swapped so the rest of the user's settings are preserved. The selection
// survives a reload because the update is persisted through the settings store. Built on
// CommunityToolkit.Mvvm and WPF-free so the behavior is driven for real in specs; the thin view binds to it.

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

	[ObservableProperty]
	private bool _isActive;

	public void OnNavigatedTo() => IsActive = true;

	public void OnNavigatedFrom() => IsActive = false;

	// Load the device list and the persisted selection through Mediator.
	[RelayCommand]
	private async Task LoadAsync(CancellationToken cancellationToken)
	{
		IReadOnlyList<AudioDeviceDto> devices = await _mediator.Send(new ListCaptureDevicesQuery(), cancellationToken);
		_settings = await _mediator.Send(new GetSettingsQuery(), cancellationToken);

		Devices.Clear();

		// Always offer "follow the OS default" as the first choice.
		Devices.Add(new AudioDeviceDto(AudioDevice.SystemDefault, "System default", IsSystemDefault: false));
		foreach (AudioDeviceDto device in devices)
		{
			Devices.Add(device);
		}

		SelectedDeviceId = _settings.CaptureDeviceId;
	}

	// Persist a new device selection by submitting the whole settings with the device swapped.
	[RelayCommand]
	private async Task SelectAsync(string? deviceId, CancellationToken cancellationToken)
	{
		if (deviceId is null || _settings is null)
		{
			return;
		}

		await _mediator.Send(new UpdateSettingsCommand(_settings with { CaptureDeviceId = deviceId }), cancellationToken);
		_settings = _settings with { CaptureDeviceId = deviceId };
		SelectedDeviceId = deviceId;
	}
}
