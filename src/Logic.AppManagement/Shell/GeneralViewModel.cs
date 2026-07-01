// The shell's General settings section: app-level preferences that aren't tied to a single subsystem.
// Today it surfaces two: "start Whisper at login" (bound to the OS startup registration through IMediator)
// and the display the recording overlay appears on. Both re-read their source on every activation so the
// section never drifts from reality, and the programmatic set a load performs is suppressed (IsLoading) so
// only a genuine user change commits — the same commit-on-genuine-change discipline as the theme switcher
// and the device picker. The overlay-display picker lists the attached monitors (ListMonitorsQuery) with a
// "Primary display (default)" choice first (persisted as null, so a fresh install and a removed display
// both fall back to the primary), and persists a change through the whole settings DTO like the other
// pickers. Built on CommunityToolkit.Mvvm and WPF-free so the behavior is driven for real in specs.

using System.Collections.ObjectModel;
using Application.Display;
using Application.Settings;
using Application.Startup;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Mediator;

namespace Logic.AppManagement.Shell;

public sealed partial class GeneralViewModel : FeatureViewModel
{
	private readonly IMediator _mediator;

	private AppSettingsDto? _settings;

	public GeneralViewModel(IMediator mediator) => _mediator = mediator;

	/// <summary>Whether Whisper is registered to launch at user login, two-way bound to the toggle.</summary>
	[ObservableProperty]
	private bool _runAtLogin;

	/// <summary>True while a load is in flight, so the programmatic sets it performs do not commit.</summary>
	[ObservableProperty]
	private bool _isLoading;

	/// <summary>The overlay-display choices: "Primary display (default)" first, then each attached monitor.</summary>
	public ObservableCollection<OverlayMonitorOption> OverlayMonitors { get; } = [];

	/// <summary>The device name of the display the overlay is placed on, or null for the primary default.
	/// Two-way bound to the picker's selected value.</summary>
	[ObservableProperty]
	private string? _selectedOverlayMonitor;

	/// <summary>The overlay display currently persisted in settings — what a selection change is compared
	/// against to tell a real user pick from the programmatic selection a reload performs.</summary>
	public string? CommittedOverlayMonitor => _settings?.OverlayMonitorDeviceName;

	// Re-sync from the real sources on EVERY activation, like the other sections: the OS Run key and the
	// attached monitors are the sources of truth, so opening the section reflects reality, not a stale snapshot.
	protected override void OnActivated() => LoadCommand.Execute(null);

	// Read the current registration state, the attached monitors, and the persisted overlay display for
	// display. The IsLoading gate keeps the programmatic sets this performs from echoing straight back as if
	// the user had changed them.
	[RelayCommand]
	private async Task LoadAsync(CancellationToken cancellationToken)
	{
		IsLoading = true;
		try
		{
			RunAtLogin = await _mediator.Send(new GetRunOnLoginQuery(), cancellationToken);

			_settings = await _mediator.Send(new GetSettingsQuery(), cancellationToken);
			OnPropertyChanged(nameof(CommittedOverlayMonitor));

			IReadOnlyList<MonitorInfo> monitors = await _mediator.Send(new ListMonitorsQuery(), cancellationToken);

			OverlayMonitors.Clear();
			// The default is always offered first and persists as null, so it survives a display being removed.
			OverlayMonitors.Add(new OverlayMonitorOption(null, "Primary display (default)"));
			foreach (MonitorInfo monitor in monitors)
			{
				// The primary is already represented by the default choice above; list the others by name.
				if (!monitor.IsPrimary)
				{
					OverlayMonitors.Add(new OverlayMonitorOption(monitor.DeviceName, monitor.FriendlyName));
				}
			}

			// Reflect the persisted selection, healing a no-longer-attached display back to the primary default
			// so the picker never shows a blank selection.
			string? persisted = _settings.OverlayMonitorDeviceName;
			bool present = persisted is null
				|| OverlayMonitors.Any(option => string.Equals(option.DeviceName, persisted, StringComparison.OrdinalIgnoreCase));
			SelectedOverlayMonitor = present ? persisted : null;
		}
		finally
		{
			IsLoading = false;
		}
	}

	// A genuine user toggle (not the load's programmatic set) applies the change through the command. Both
	// port operations are idempotent, so a repeated state never duplicates or orphans the registration.
	partial void OnRunAtLoginChanged(bool value)
	{
		if (IsLoading)
		{
			return;
		}

		_ = _mediator.Send(new SetRunOnLoginCommand(value));
	}

	// A genuine user pick (not the load's programmatic selection) persists the chosen display by submitting
	// the whole settings DTO with the overlay monitor swapped, so the rest of the user's settings are
	// preserved. UpdateSettings publishes the change so the overlay moves live, without a restart.
	partial void OnSelectedOverlayMonitorChanged(string? value)
	{
		if (IsLoading || _settings is null || _settings.OverlayMonitorDeviceName == value)
		{
			return;
		}

		_settings = _settings with { OverlayMonitorDeviceName = value };
		_ = _mediator.Send(new UpdateSettingsCommand(_settings));
		OnPropertyChanged(nameof(CommittedOverlayMonitor));
	}
}
