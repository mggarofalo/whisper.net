// The shell's model view: a thin view bound to ModelViewModel that shows the active model
// id and triggers its Mediator-backed Refresh command. Pure view glue — the data and the Mediator call
// live in the WPF-free view-model — so it is verified by smoke, not by the specs.

using System.Windows.Controls;

namespace Presentation.Shell.Views;

public partial class ModelView : UserControl
{
	public ModelView() => InitializeComponent();
}
