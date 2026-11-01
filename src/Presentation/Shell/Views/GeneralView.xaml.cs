// The shell's General view: a thin view bound to GeneralViewModel that surfaces the start-at-login
// toggle, the overlay-display picker, and the version + update controls. Pure view glue — the load, the
// commit-on-change and the update commands live in the WPF-free view-model — so it is verified by smoke,
// not by the specs.

using System.Windows.Controls;

namespace Presentation.Shell.Views;

public partial class GeneralView : UserControl
{
	public GeneralView() => InitializeComponent();
}
