// The shell's General settings section: app-level preferences that aren't tied to a single
// subsystem. Today it surfaces one toggle — "start Whisper at login" — bound to the OS startup
// registration through IMediator (GetRunOnLoginQuery / SetRunOnLoginCommand), so the user never has
// to relaunch the app by hand after a reboot. It re-reads the real registration on every activation
// (IStartupRegistration is the source of truth, so the toggle never drifts from reality), and the
// programmatic set a load performs is suppressed (IsLoading) so only a genuine user toggle commits —
// the same commit-on-genuine-change discipline as the theme switcher and the device picker. Built on
// CommunityToolkit.Mvvm and WPF-free so the behavior is driven for real in specs; the thin view binds.

using Application.Startup;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Mediator;

namespace Logic.AppManagement.Shell;

public sealed partial class GeneralViewModel : FeatureViewModel
{
	private readonly IMediator _mediator;

	public GeneralViewModel(IMediator mediator) => _mediator = mediator;

	/// <summary>Whether Whisper is registered to launch at user login, two-way bound to the toggle.</summary>
	[ObservableProperty]
	private bool _runAtLogin;

	/// <summary>True while a load is in flight, so the programmatic set it performs does not commit.</summary>
	[ObservableProperty]
	private bool _isLoading;

	// Re-sync from the real registration on EVERY activation, like the hotkey section: the OS Run key is
	// the source of truth (it could have been set on a prior run or cleared externally), so opening the
	// section always reflects reality rather than a stale snapshot.
	protected override void OnActivated() => LoadCommand.Execute(null);

	// Read the current registration state for display. The IsLoading gate keeps the programmatic set this
	// performs from echoing the value straight back to the registration as if the user had toggled it.
	[RelayCommand]
	private async Task LoadAsync(CancellationToken cancellationToken)
	{
		IsLoading = true;
		try
		{
			RunAtLogin = await _mediator.Send(new GetRunOnLoginQuery(), cancellationToken);
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
}
