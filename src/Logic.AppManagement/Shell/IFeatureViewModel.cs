// The activation lifecycle a feature view-model opts into so the navigation service can tell it when it
// becomes — or stops being — the shell's active content (WHISPER-19). A view-model implements this to
// start work on entry (e.g. load its data) and release/quiesce on exit, which is how the shell
// "activates the correct view-model and deactivates the previous one" without the views owning that
// orchestration. Kept WPF-free in Logic so the navigation behavior is driven for real in specs.

namespace Logic.AppManagement.Shell;

public interface IFeatureViewModel
{
	/// <summary>Called when this view-model becomes the shell's active content.</summary>
	void OnNavigatedTo();

	/// <summary>Called when the shell navigates away from this view-model to another section.</summary>
	void OnNavigatedFrom();
}
