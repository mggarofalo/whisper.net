// Port for surfacing the app's window to the user (WHISPER-18). Implemented in Presentation (the WPF
// shell) and faked in specs. Kept deliberately small: the tray "Open Settings" action and, later,
// single-instance activation (WHISPER-25) both just need to bring the settings window to the
// foreground, so they share this one seam rather than each reaching into WPF.

namespace Application.Ports;

public interface IShellPresenter
{
	/// <summary>Shows (or focuses, if already open) the settings window on the UI thread.</summary>
	void ShowSettings();
}
