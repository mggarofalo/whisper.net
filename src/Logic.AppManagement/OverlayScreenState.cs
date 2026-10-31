// What the overlay window really looks like on screen, read back from Win32/DWM after each present
// (WHISPER-143). "Overlay shown" used to be logged from WPF's own visibility flag while the user saw
// nothing, so the window now reads back the native truth and this pure, WPF-free verdict judges it: only a
// window that is visible, uncloaked, on the current virtual desktop, top-most and uncovered is on screen.
// The Reason names the first thing wrong so one log line explains a miss on the user's machine.

namespace Logic.AppManagement;

/// <param name="Visible">The HWND is visible (IsWindowVisible).</param>
/// <param name="Cloaked">DWM reports the window cloaked (hidden by the shell even though it is "visible").</param>
/// <param name="OnCurrentDesktop">Whether the window is on the active virtual desktop; null when the shell could not say.</param>
/// <param name="Topmost">The window carries WS_EX_TOPMOST.</param>
/// <param name="CoveredBy">The first visible window above the overlay that overlaps it, or null when nothing covers it.</param>
public readonly record struct OverlayScreenState(
	bool Visible,
	bool Cloaked,
	bool? OnCurrentDesktop,
	bool Topmost,
	string? CoveredBy)
{
	/// <summary>True when the user can actually see the overlay.</summary>
	public bool IsOnScreen => Problem is null;

	/// <summary>"on screen", or the first problem found, in the order that best explains a miss.</summary>
	public string Reason => Problem ?? "on screen";

	// Ordered most-specific first: a window on another virtual desktop is ALSO cloaked, so the desktop
	// mismatch is named before the generic cloak.
	private string? Problem =>
		!Visible ? "not visible"
		: OnCurrentDesktop == false ? "on another virtual desktop"
		: Cloaked ? "cloaked"
		: !Topmost ? "not top-most"
		: CoveredBy is not null ? $"covered by {CoveredBy}"
		: null;
}
