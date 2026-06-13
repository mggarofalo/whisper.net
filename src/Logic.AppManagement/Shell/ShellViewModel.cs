// The dashboard shell's view-model: the navigable main window's content host. It exposes
// the registered section keys for the nav region, a NavigateCommand the nav buttons invoke, and the
// CurrentViewModel the window's content region binds to. All navigation is delegated to the
// INavigationService, which resolves each feature view-model from the DI container — so the shell holds
// no feature logic and no direct port/handler references, only the MVVM plumbing. Built on
// CommunityToolkit.Mvvm ([ObservableProperty]/[RelayCommand]) and kept WPF-free so it is unit-testable.

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Logic.AppManagement.Shell;

public sealed partial class ShellViewModel : ObservableObject
{
	private readonly INavigationService _navigation;

	public ShellViewModel(INavigationService navigation, ThemeViewModel theme)
	{
		_navigation = navigation;
		Theme = theme;
		_navigation.CurrentViewModelChanged += OnCurrentViewModelChanged;

		// Open the shell on its first registered section so the window always has content on show.
		if (_navigation.Sections.Count > 0)
		{
			string first = _navigation.Sections[0];
			_navigation.NavigateTo(first);
			CurrentSectionKey = first;
		}

		// Load the persisted theme so the sidebar-footer switcher shows the current choice on open
		//. The shell is created once, on the UI thread, so this runs at startup.
		Theme.LoadCommand.Execute(null);
	}

	/// <summary>The theme switcher shown in the sidebar footer.</summary>
	public ThemeViewModel Theme { get; }

	/// <summary>The feature view-model currently shown in the shell's content region.</summary>
	[ObservableProperty]
	private object? _currentViewModel;

	/// <summary>The key of the section currently shown, so the nav region can mark the active item
	///. NavigateTo is only ever driven from here, so tracking the key here is authoritative.</summary>
	[ObservableProperty]
	private string? _currentSectionKey;

	/// <summary>The section keys the nav region renders, in registration order.</summary>
	public IReadOnlyList<string> Sections => _navigation.Sections;

	/// <summary>Navigates the shell to the named section; bound to the nav region's buttons.</summary>
	[RelayCommand]
	private void Navigate(string sectionKey)
	{
		_navigation.NavigateTo(sectionKey);
		CurrentSectionKey = sectionKey;
	}

	private void OnCurrentViewModelChanged(object? sender, EventArgs e) =>
		CurrentViewModel = _navigation.CurrentViewModel;
}
