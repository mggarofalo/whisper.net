// The shell's model view (WHISPER-19): a thin view bound to ModelViewModel that shows the active model
// id and triggers its Mediator-backed Refresh command. Pure view glue — the data and the Mediator call
// live in the WPF-free view-model — so it is verified by smoke, not by the specs. WHISPER-27 grows this
// into the full model picker.

using System.Windows.Controls;

namespace Presentation.Shell.Views;

public partial class ModelView : UserControl
{
	public ModelView() => InitializeComponent();
}
