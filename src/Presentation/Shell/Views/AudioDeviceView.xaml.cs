// The shell's audio device view (WHISPER-33): a thin view bound to AudioDeviceViewModel that lists input
// devices and persists the selection. Pure view glue — the query, persistence, and selection live in the
// WPF-free view-model — so it is verified by smoke, not by the specs.

using System.Windows.Controls;

namespace Presentation.Shell.Views;

public partial class AudioDeviceView : UserControl
{
	public AudioDeviceView() => InitializeComponent();
}
