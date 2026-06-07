// IShellPresenter for the WPF shell (WHISPER-18): shows — or focuses, if already open — the single
// settings window, marshaled onto the UI thread. Both the tray "Open Settings" action and (WHISPER-25)
// single-instance activation surface the window through this one seam.

using System.Linq;
using System.Windows;
using Application.Ports;

namespace Presentation.Shell;

public sealed class WpfShellPresenter : IShellPresenter
{
	public void ShowSettings() =>
		System.Windows.Application.Current.Dispatcher.Invoke(() =>
		{
			System.Windows.Application application = System.Windows.Application.Current;
			SettingsWindow window = application.Windows.OfType<SettingsWindow>().FirstOrDefault() ?? new SettingsWindow();

			window.Show();
			if (window.WindowState == WindowState.Minimized)
			{
				window.WindowState = WindowState.Normal;
			}

			window.Activate();
		});
}
