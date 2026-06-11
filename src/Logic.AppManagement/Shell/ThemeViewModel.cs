// The theme switcher's view-model (WHISPER-121): exposes the System / Light / Dark choices and the
// persisted selection for the sidebar footer to bind. It depends on nothing but IMediator — it loads via
// GetSettingsQuery and persists a change via UpdateSettingsCommand (carrying the whole settings DTO with
// the theme swapped, so the rest of the user's settings are preserved). Persisting publishes the change on
// the instant-apply channel (WHISPER-78), which the App applies to WPF's ThemeMode live. The
// programmatic selection a load performs is suppressed (IsLoading) so only a genuine user pick commits —
// the same commit-on-genuine-change discipline as the audio-device picker. Built on CommunityToolkit.Mvvm
// and WPF-free so the behavior is driven for real in specs; the thin footer control binds to it.

using Application.Settings;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Domain.Settings;
using Mediator;

namespace Logic.AppManagement.Shell;

public sealed partial class ThemeViewModel : ObservableObject
{
	private readonly IMediator _mediator;

	private AppSettingsDto? _settings;

	public ThemeViewModel(IMediator mediator) => _mediator = mediator;

	/// <summary>The selectable theme choices, in display order.</summary>
	public IReadOnlyList<ThemePreference> Themes { get; } =
		[ThemePreference.System, ThemePreference.Light, ThemePreference.Dark];

	/// <summary>The current theme preference, two-way bound to the switcher.</summary>
	[ObservableProperty]
	private ThemePreference _selectedTheme;

	/// <summary>True while a load is in flight, so the programmatic selection it performs does not commit.</summary>
	[ObservableProperty]
	private bool _isLoading;

	// Load the persisted preference for display. Refresh stays the implicit re-read on activation.
	[RelayCommand]
	private async Task LoadAsync(CancellationToken cancellationToken)
	{
		IsLoading = true;
		try
		{
			_settings = await _mediator.Send(new GetSettingsQuery(), cancellationToken);
			if (_settings is not null)
			{
				SelectedTheme = _settings.ThemePreference;
			}
		}
		finally
		{
			IsLoading = false;
		}
	}

	// A genuine user pick (not the load's programmatic selection) persists the new theme; UpdateSettings
	// publishes the change so the App applies the new ThemeMode without a restart.
	partial void OnSelectedThemeChanged(ThemePreference value)
	{
		if (IsLoading || _settings is null || _settings.ThemePreference == value)
		{
			return;
		}

		_settings = _settings with { ThemePreference = value };
		_ = _mediator.Send(new UpdateSettingsCommand(_settings));
	}
}
