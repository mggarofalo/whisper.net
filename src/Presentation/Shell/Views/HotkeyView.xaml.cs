// The shell's hotkey view (WHISPER-33): a thin view bound to HotkeyViewModel that shows the current
// binding, assigns a new one, and surfaces a validation error. Pure view glue — load/assign/validation
// live in the WPF-free view-model — so it is verified by smoke, not by the specs.

using System.Windows.Controls;

namespace Presentation.Shell.Views;

public partial class HotkeyView : UserControl
{
	public HotkeyView() => InitializeComponent();
}
