// The Win32 plumbing that makes the overlay behave like a real HUD overlay rather than a normal window:
// genuinely click-through at the OS level (WS_EX_TRANSPARENT, so clicks fall through to the app below
// instead of being swallowed), never activated or focus-stealing (WS_EX_NOACTIVATE), absent from Alt-Tab
// and the taskbar (WS_EX_TOOLWINDOW), and reliably above every other top-most window (an explicit
// HWND_TOPMOST SetWindowPos re-asserted on each show). The last point is the durable fix for the overlay
// "not appearing": a ShowInTaskbar=false WPF window gets a hidden owner that may not carry WS_EX_TOPMOST,
// so Topmost=true alone can leave the overlay rendering BEHIND the focused app. This is the single place
// that touches the window's native styles; the pure placement math is OverlayPlacement in Logic.

using System;
using System.Runtime.InteropServices;

namespace Presentation.Overlay;

internal static class OverlayWindowInterop
{
	private const int GwlExStyle = -20;

	// Extended window styles. LAYERED is already set by AllowsTransparency; TRANSPARENT adds OS-level
	// click-through, NOACTIVATE keeps the overlay from ever taking focus, TOOLWINDOW hides it from Alt-Tab.
	private const long WsExTransparent = 0x00000020;
	private const long WsExToolWindow = 0x00000080;
	private const long WsExLayered = 0x00080000;
	private const long WsExNoActivate = 0x08000000;

	private static readonly nint HwndTopmost = new(-1);

	private const uint SwpNoMove = 0x0002;
	private const uint SwpNoSize = 0x0001;
	private const uint SwpNoActivate = 0x0010;

	// Add the overlay's extended styles once the HWND exists (SourceInitialized). Layered is already on via
	// AllowsTransparency; the OR is idempotent, so re-applying is harmless.
	public static void MakeOverlayStyled(nint hwnd)
	{
		long exStyle = (long)GetWindowLongPtr(hwnd, GwlExStyle);
		exStyle |= WsExTransparent | WsExNoActivate | WsExToolWindow | WsExLayered;
		_ = SetWindowLongPtr(hwnd, GwlExStyle, new nint(exStyle));
	}

	// Force the window above every other top-most window without activating it. Re-asserted on each show so
	// the overlay wins even when a full-screen or top-most app claimed the top of the Z-order after startup.
	public static void BringToTopmost(nint hwnd) =>
		SetWindowPos(hwnd, HwndTopmost, 0, 0, 0, 0, SwpNoMove | SwpNoSize | SwpNoActivate);

	[DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW", SetLastError = true)]
	private static extern nint GetWindowLongPtr(nint hWnd, int nIndex);

	[DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)]
	private static extern nint SetWindowLongPtr(nint hWnd, int nIndex, nint dwNewLong);

	[DllImport("user32.dll", SetLastError = true)]
	[return: MarshalAs(UnmanagedType.Bool)]
	private static extern bool SetWindowPos(nint hWnd, nint hWndInsertAfter, int x, int y, int cx, int cy, uint uFlags);
}
