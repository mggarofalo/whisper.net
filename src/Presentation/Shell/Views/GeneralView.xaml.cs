// The shell's General view: a thin view bound to GeneralViewModel that surfaces the
// start-at-login toggle. Pure view glue — the load and the commit-on-change live in the WPF-free
// view-model — so it is verified by smoke, not by the specs.

using System.Windows.Controls;

namespace Presentation.Shell.Views;

public partial class GeneralView : UserControl
{
	public GeneralView() => InitializeComponent();
}
