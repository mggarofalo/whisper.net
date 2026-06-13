// The dashboard shell window: the navigable main window the tray "Open Settings" surfaces.
// It is pure view glue — it hosts a navigation region and a content region bound to the injected
// ShellViewModel, whose NavigationService resolves each feature view-model from the DI container. All
// behaviour lives in the WPF-free view-models, so the window is verified by smoke, not by the specs.

using Logic.AppManagement.Shell;

namespace Presentation.Shell;

public partial class ShellWindow : System.Windows.Window
{
	public ShellWindow(ShellViewModel viewModel)
	{
		InitializeComponent();
		DataContext = viewModel;
	}
}
