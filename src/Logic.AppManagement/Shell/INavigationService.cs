// The shell's navigation seam (WHISPER-19): it owns which feature view-model is the active content and
// switches between the registered sections. Navigating resolves the target view-model from the DI
// container by its section key, activates it, and deactivates the previously active one so exactly one
// feature view-model is live at a time. The shell view-model binds to this; the thin WPF window binds
// to the shell view-model. Kept WPF-free so the navigation behavior is driven for real in specs.

namespace Logic.AppManagement.Shell;

public interface INavigationService
{
	/// <summary>The feature view-model currently shown as the shell's content, or null before first navigation.</summary>
	object? CurrentViewModel { get; }

	/// <summary>The keys of the registered sections, in registration order, for the shell's nav region.</summary>
	IReadOnlyList<string> Sections { get; }

	/// <summary>Raised after <see cref="CurrentViewModel"/> changes, so the shell can refresh its content.</summary>
	event EventHandler? CurrentViewModelChanged;

	/// <summary>
	/// Navigates to the section registered under <paramref name="sectionKey"/>: resolves its view-model
	/// from the container, makes it the current content, and deactivates the previous one. Throws when no
	/// section is registered for the key.
	/// </summary>
	void NavigateTo(string sectionKey);
}
