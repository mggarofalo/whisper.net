// The shell's history view: a thin view bound to HistoryViewModel that lists past
// transcriptions, pages through them, copies one, and shows an empty state. Pure view glue — querying,
// paging, and copying live in the WPF-free view-model — so it is verified by smoke, not by the specs.

using System.Windows.Controls;

namespace Presentation.Shell.Views;

public partial class HistoryView : UserControl
{
	public HistoryView() => InitializeComponent();
}
