// The settings window. A placeholder shell in M6 (so the tray's "Open Settings" and single-instance
// activation have a real window to surface); the actual settings UI is built iteratively in M10.

using System.Windows;

namespace Presentation.Shell;

public partial class SettingsWindow : Window
{
	public SettingsWindow() => InitializeComponent();
}
