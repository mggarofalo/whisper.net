// The shell's hotkey section (WHISPER-33): shows the current dictation hotkey and assigns a new one. It
// depends on nothing but IMediator — it loads via GetSettingsQuery and saves via UpdateSettingsCommand,
// carrying the whole settings DTO with the hotkey swapped. An invalid (empty or unbindable) chord is
// rejected by the settings validator in the Mediator pipeline before anything persists; the ViewModel
// surfaces that as an error and leaves the current binding unchanged. Built on CommunityToolkit.Mvvm and
// WPF-free so the behavior is driven for real in specs; the thin view binds to it (capturing the keypress
// is view glue).

using Application.Settings;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FluentValidation;
using Mediator;

namespace Logic.AppManagement.Shell;

public sealed partial class HotkeyViewModel : ObservableObject, IFeatureViewModel
{
	private readonly IMediator _mediator;

	private AppSettingsDto? _settings;

	public HotkeyViewModel(IMediator mediator) => _mediator = mediator;

	/// <summary>The current dictation hotkey chord (e.g. "Ctrl+Shift+D").</summary>
	[ObservableProperty]
	private string? _currentHotkey;

	/// <summary>A validation error from the last assignment attempt, or null when the last attempt succeeded.</summary>
	[ObservableProperty]
	private string? _error;

	[ObservableProperty]
	private bool _isActive;

	public void OnNavigatedTo() => IsActive = true;

	public void OnNavigatedFrom() => IsActive = false;

	// Load the persisted hotkey through Mediator.
	[RelayCommand]
	private async Task LoadAsync(CancellationToken cancellationToken)
	{
		_settings = await _mediator.Send(new GetSettingsQuery(), cancellationToken);
		CurrentHotkey = _settings.Hotkey;
		Error = null;
	}

	// Assign a new hotkey by submitting the whole settings with the chord swapped. A chord the validator
	// rejects leaves the current binding unchanged and surfaces the error.
	[RelayCommand]
	private async Task AssignAsync(string? chord, CancellationToken cancellationToken)
	{
		if (_settings is null)
		{
			return;
		}

		try
		{
			await _mediator.Send(new UpdateSettingsCommand(_settings with { Hotkey = chord ?? string.Empty }), cancellationToken);
			_settings = _settings with { Hotkey = chord ?? string.Empty };
			CurrentHotkey = chord;
			Error = null;
		}
		catch (ValidationException exception)
		{
			Error = exception.Errors.FirstOrDefault()?.ErrorMessage ?? "Invalid hotkey.";
		}
	}
}
