// The formalized activation lifecycle for cached feature view-models (WHISPER-94). Sections are cached
// per shell UI scope (WHISPER-89), so navigation toggles activation instead of recreating: the
// navigation service calls OnNavigatedTo/OnNavigatedFrom, this base flips IsActive exactly once per
// transition, and the OnActivated/OnDeactivated hooks are where a view-model registers and removes its
// live subscriptions (messenger registrations, controller events). The rule: an inactive cached
// view-model holds no live subscriptions and gets no callbacks; instances are disposed only when the
// shell's UI scope tears down. See docs/architecture.md ("View-model activation lifecycle").

using CommunityToolkit.Mvvm.ComponentModel;

namespace Logic.AppManagement.Shell;

public abstract partial class FeatureViewModel : ObservableValidator, IFeatureViewModel
{
	/// <summary>Whether this section is the shell's active content; toggled by the navigation lifecycle.</summary>
	[ObservableProperty]
	private bool _isActive;

	public void OnNavigatedTo()
	{
		if (IsActive)
		{
			return;
		}

		IsActive = true;
		OnActivated();
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
