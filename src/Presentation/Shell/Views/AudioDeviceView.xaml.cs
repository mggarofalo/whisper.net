// The shell's audio device view (ComboBox): a thin view bound to
// AudioDeviceViewModel that lists input devices in a ComboBox and persists the selection. Pure view
// glue — the query, persistence, fallback handling, and the commit-on-genuine-user-pick decision all
// live in the WPF-free view-model, so this code-behind is InitializeComponent-only and
// the view is verified by smoke, not by the specs.

using System.Windows.Controls;

namespace Presentation.Shell.Views;

public partial class AudioDeviceView : UserControl
{
	public AudioDeviceView() => InitializeComponent();
}
