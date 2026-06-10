// The shell's landing section (WHISPER-19): the overview the dashboard opens on before the user picks a
// feature area. It carries no Application dependencies — it exists so the shell has a default content
// view and so navigating away from it exercises the activate/deactivate lifecycle. Built on
// CommunityToolkit.Mvvm and kept WPF-free so it is unit-testable; later M10 issues flesh out the
// overview (e.g. surfacing quick stats).

using CommunityToolkit.Mvvm.ComponentModel;

namespace Logic.AppManagement.Shell;

public sealed partial class HomeViewModel : ObservableValidator, IFeatureViewModel
{
	/// <summary>Whether this section is the shell's active content; toggled by the navigation lifecycle.</summary>
	[ObservableProperty]
	private bool _isActive;

	public void OnNavigatedTo() => IsActive = true;

	public void OnNavigatedFrom() => IsActive = false;
}
