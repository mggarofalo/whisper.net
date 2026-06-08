// The shell's landing view (WHISPER-19): a thin overview bound to HomeViewModel. Pure view glue — all
// state lives in the WPF-free view-model — so it is verified by smoke, not by the specs.

using System.Windows.Controls;

namespace Presentation.Shell.Views;

public partial class HomeView : UserControl
{
	public HomeView() => InitializeComponent();
}
