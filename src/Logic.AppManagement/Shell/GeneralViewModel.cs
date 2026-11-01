// The shell's General settings section: app-level preferences that aren't tied to a single subsystem.
// Today it surfaces two: "start Whisper at login" (bound to the OS startup registration through IMediator)
// and the display the recording overlay appears on. Both re-read their source on every activation so the
// section never drifts from reality, and the programmatic set a load performs is suppressed (IsLoading) so
// only a genuine user change commits — the same commit-on-genuine-change discipline as the theme switcher
// and the device picker. The overlay-display picker lists the attached monitors (ListMonitorsQuery) with a
// "Primary display (default)" choice first (persisted as null, so a fresh install and a removed display
// both fall back to the primary), and persists a change through the whole settings DTO like the other
// pickers.
//
// It also hosts "About & updates" (WHISPER-151): the running version, a "Check for updates" button, a
// "Download and restart" button once a newer release is found, and the opt-in automatic startup check.
// Opening the section never touches the network — only the user's click sends CheckForUpdateQuery — and
// the automatic check is a persisted setting that is off by default. Built on CommunityToolkit.Mvvm and
// WPF-free so the behavior is driven for real in specs.

using System.Collections.ObjectModel;
using Application.Display;
using Application.Settings;
using Application.Startup;
using Application.Updates;
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

	/// <summary>The running version, formatted as the version plus a short commit hash.</summary>
	[ObservableProperty]
	private string _appVersion = string.Empty;

	/// <summary>Whether the app checks for updates at startup. Opt-in: off by default.</summary>
	[ObservableProperty]
	private bool _automaticUpdateChecks;

	/// <summary>What the last check or install found, for the status line beside the buttons.</summary>
	[ObservableProperty]
	private string _updateStatus = string.Empty;

	/// <summary>The newer version the last check found, or null when there is nothing to install.</summary>
	[ObservableProperty]
	[NotifyPropertyChangedFor(nameof(IsUpdateAvailable))]
	[NotifyCanExecuteChangedFor(nameof(InstallUpdateCommand))]
	private string? _availableVersion;

	/// <summary>True while a check or an install is in flight, so the buttons cannot start a second one.</summary>
	[ObservableProperty]
	[NotifyCanExecuteChangedFor(nameof(CheckForUpdatesCommand))]
	[NotifyCanExecuteChangedFor(nameof(InstallUpdateCommand))]
	private bool _isUpdating;

	/// <summary>Download progress of an install, 0-100.</summary>
	[ObservableProperty]
	private int _updateProgress;

	/// <summary>Whether a newer release was found and can be installed.</summary>
	public bool IsUpdateAvailable => AvailableVersion is not null;

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
			AutomaticUpdateChecks = _settings.AutomaticUpdateChecksEnabled;

			AppVersion = await _mediator.Send(new GetAppVersionQuery(), cancellationToken);

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

	// The user's explicit request to contact the release feed. A failure is reported in the status line;
	// nothing is downloaded until the user also clicks "Download and restart".
	[RelayCommand(CanExecute = nameof(CanCheckForUpdates))]
	private async Task CheckForUpdatesAsync(CancellationToken cancellationToken)
	{
		IsUpdating = true;
		AvailableVersion = null;
		UpdateStatus = "Checking for updates…";
		try
		{
			UpdateCheckResult result = await _mediator.Send(new CheckForUpdateQuery(), cancellationToken);
			AvailableVersion = result.Status == UpdateCheckStatus.Available ? result.Version : null;
			UpdateStatus = result.Status switch
			{
				UpdateCheckStatus.Available => $"Version {result.Version} is available.",
				UpdateCheckStatus.UpToDate => "Whisper is up to date.",
				UpdateCheckStatus.NotInstalled => "Updates are only available in the installed app.",
				_ => $"Couldn't check for updates: {Sentence(result.Error)}",
			};
		}
		finally
		{
			IsUpdating = false;
		}
	}

	private bool CanCheckForUpdates() => !IsUpdating;

	// Download the release the check found, then restart into it. On success the app is already shutting
	// down; on failure the status explains why and the app keeps running on the current version.
	[RelayCommand(CanExecute = nameof(CanInstallUpdate))]
	private async Task InstallUpdateAsync(CancellationToken cancellationToken)
	{
		if (AvailableVersion is not { } version)
		{
			return;
		}

		IsUpdating = true;
		UpdateProgress = 0;
		UpdateStatus = $"Downloading version {version}…";
		bool restarting = false;
		try
		{
			Progress<int> progress = new(percent => UpdateProgress = percent);
			UpdateInstallResult result = await _mediator.Send(new InstallUpdateCommand(version, progress), cancellationToken);
			restarting = result.Restarting;
			UpdateStatus = restarting
				? $"Restarting to finish installing version {version}…"
				: $"Couldn't install the update: {Sentence(result.Error)}";
		}
		finally
		{
			// While the app shuts down to apply the update, keep the buttons disabled.
			IsUpdating = restarting;
		}
	}

	private bool CanInstallUpdate() => !IsUpdating && AvailableVersion is not null;

	// Error messages come from the network stack; end them with exactly one full stop.
	private static string Sentence(string? message) =>
		string.IsNullOrWhiteSpace(message) ? "an unknown error occurred." : message.Trim().TrimEnd('.') + ".";

	// A genuine user toggle persists the opt-in; it takes effect at the next startup.
	partial void OnAutomaticUpdateChecksChanged(bool value)
	{
		if (IsLoading || _settings is null || _settings.AutomaticUpdateChecksEnabled == value)
		{
			return;
		}

		_settings = _settings with { AutomaticUpdateChecksEnabled = value };
		_ = PersistAutomaticUpdateChecksAsync(value);
	}

	private async Task PersistAutomaticUpdateChecksAsync(bool value) =>
		_settings = await SettingsWriter.UpdateAsync(_mediator, current => current with { AutomaticUpdateChecksEnabled = value });

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
		_ = PersistOverlayMonitorAsync(value);
		OnPropertyChanged(nameof(CommittedOverlayMonitor));
	}

	// Re-read before writing so the overlay pick never reverts a field changed elsewhere since this
	// section loaded (WHISPER-142).
	private async Task PersistOverlayMonitorAsync(string? value) =>
		_settings = await SettingsWriter.UpdateAsync(_mediator, current => current with { OverlayMonitorDeviceName = value });
}
