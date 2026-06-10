// The shell's audio device view (WHISPER-33; ComboBox in WHISPER-80): a thin view bound to
// AudioDeviceViewModel that lists input devices in a ComboBox and persists the selection. Pure view glue —
// the query, persistence, fallback handling, and selection live in the WPF-free view-model — so it is
// verified by smoke, not by the specs. The only logic here is recognizing a genuine user pick (commit it)
// versus the programmatic selection a reload performs (ignore it), using the view-model's IsLoading flag and
// its committed device id.

using System.Windows.Controls;
using Logic.AppManagement.Shell;

namespace Presentation.Shell.Views;

public partial class AudioDeviceView : UserControl
{
	public AudioDeviceView() => InitializeComponent();

	private void OnDeviceSelectionChanged(object sender, SelectionChangedEventArgs e)
	{
		// Commit only a real user pick: not while a load is repopulating the list, and only when the chosen
		// device actually differs from what is already persisted.
		if (DataContext is AudioDeviceViewModel viewModel
			&& !viewModel.IsLoading
			&& DeviceBox.SelectedValue is string deviceId
			&& deviceId != viewModel.CommittedDeviceId)
		{
			viewModel.SelectCommand.Execute(deviceId);
		}
	}
}
