// The shell's model view: a thin view bound to ModelViewModel. The models are a single-selection list whose
// selection is the model in use; choosing a row goes through CommandedSelectionBehavior to SelectCommand. Pure
// view glue — the behavior lives in the WPF-free view-model, driven by the specs — so it is verified by smoke.

using System.Windows.Controls;

namespace Presentation.Shell.Views;

public partial class ModelView : UserControl
{
	public ModelView() => InitializeComponent();
}
