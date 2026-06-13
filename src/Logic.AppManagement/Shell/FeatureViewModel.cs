// The formalized activation lifecycle for cached feature view-models. Sections are cached
// per shell UI scope, so navigation toggles activation instead of recreating: the
// navigation service calls OnNavigatedTo/OnNavigatedFrom, this base flips IsActive exactly once per
// transition, and the OnActivated/OnDeactivated hooks are where a view-model registers and removes its
// live subscriptions (messenger registrations, controller events). The rule: an inactive cached
// view-model holds no live subscriptions and gets no callbacks; instances are disposed only when the
// shell's UI scope tears down. Data sections also auto-load on FIRST activation via the
// FirstActivationLoadCommand hook, so no section opens empty waiting for a manual Refresh; the
// once-per-instance guard keeps cached sections from re-querying on every tab switch.
// See docs/architecture.md ("View-model activation lifecycle").

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Logic.AppManagement.Shell;

public abstract partial class FeatureViewModel : ObservableValidator, IFeatureViewModel
{
	/// <summary>Whether this section is the shell's active content; toggled by the navigation lifecycle.</summary>
	[ObservableProperty]
	private bool _isActive;

	// Per-instance (the cache is per shell UI scope): set before the command runs so even a
	// re-entrant activation could never queue a second auto-load.
	private bool _hasAutoLoaded;

	/// <summary>
	/// The load command the base executes once, on this section's FIRST activation, so a
	/// data section opens populated instead of empty. Later activations of the cached instance do not
	/// re-query — Refresh stays the explicit manual re-query. Sections that must re-sync on EVERY
	/// activation (the hotkey section) keep their own OnActivated trigger and leave this null.
	/// </summary>
	protected virtual IAsyncRelayCommand? FirstActivationLoadCommand => null;

	public void OnNavigatedTo()
	{
		if (IsActive)
		{
			return;
		}

		IsActive = true;
		OnActivated();

		// After OnActivated, so the section's live subscriptions exist before its first load runs.
		if (!_hasAutoLoaded && FirstActivationLoadCommand is { } load)
		{
			_hasAutoLoaded = true;
			load.Execute(null);
		}
	}

	public void OnNavigatedFrom()
	{
		if (!IsActive)
		{
			return;
		}

		IsActive = false;
		OnDeactivated();
	}

	/// <summary>Register live subscriptions here; called once each time this section becomes active.</summary>
	protected virtual void OnActivated()
	{
	}

	/// <summary>Remove live subscriptions here; called once each time the shell navigates away.</summary>
	protected virtual void OnDeactivated()
	{
	}
}
