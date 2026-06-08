// The shell's stats view (WHISPER-53): a thin view bound to StatsViewModel that shows the headline usage
// figures and a refresh action. Pure view glue — the query and the (Application-side) aggregation live
// behind the WPF-free view-model — so it is verified by smoke, not by the specs.

using System.Windows.Controls;

namespace Presentation.Shell.Views;

public partial class StatsView : UserControl
{
	public StatsView() => InitializeComponent();
}
