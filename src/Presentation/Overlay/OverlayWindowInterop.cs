// The Win32 plumbing that makes the overlay behave like a real HUD overlay rather than a normal window:
// genuinely click-through at the OS level (WS_EX_TRANSPARENT, so clicks fall through to the app below
// instead of being swallowed), never activated or focus-stealing (WS_EX_NOACTIVATE), absent from Alt-Tab
// and the taskbar (WS_EX_TOOLWINDOW), and reliably above every other top-most window (an explicit
// HWND_TOPMOST SetWindowPos re-asserted on each show). The last point is the durable fix for the overlay
// "not appearing": a ShowInTaskbar=false WPF window gets a hidden owner that may not carry WS_EX_TOPMOST,
// so Topmost=true alone can leave the overlay rendering BEHIND the focused app. This is the single place
// that touches the window's native styles; the pure placement math is OverlayPlacement in Logic.

using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using Logic.AppManagement;

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

	// The recovery nudge after a failed on-screen check: re-assert top-most AND ask the window manager to
	// show it, still without activating it or touching WPF's own Visibility (which is data-bound).
	public static void ForceShowTopmost(nint hwnd) =>
		SetWindowPos(hwnd, HwndTopmost, 0, 0, 0, 0, SwpNoMove | SwpNoSize | SwpNoActivate | SwpShowWindow);

	// A tool window belongs to the virtual desktop it was first shown on and is cloaked on every other one,
	// so an overlay realized on desktop 1 is invisible on desktop 2 while WPF still reports it visible
	// (WHISPER-143). Move it to the desktop of the foreground window (always the current one). Returns
	// true when it moved, false when it was already there, null when the shell could not say (no foreground
	// window, no virtual-desktop support, a COM failure) — never throws.
	public static bool? MoveToCurrentDesktop(nint hwnd)
	{
		try
		{
			IVirtualDesktopManager? manager = VirtualDesktops;
			if (manager is null || manager.IsWindowOnCurrentVirtualDesktop(hwnd, out int onCurrent) != 0)
			{
				return null;
			}

			if (onCurrent != 0)
			{
				return false;
			}

			nint foreground = GetForegroundWindow();
			if (foreground == nint.Zero || foreground == hwnd
				|| manager.GetWindowDesktopId(foreground, out Guid desktop) != 0 || desktop == Guid.Empty)
			{
				return null;
			}

			return manager.MoveWindowToDesktop(hwnd, ref desktop) == 0 ? true : null;
		}
		catch (COMException)
		{
			return null;
		}
	}

	// Read back what the window really looks like on screen — the native truth, not WPF's IsVisible flag —
	// for the pure OverlayScreenState verdict. Never throws: a failed probe reads as the optimistic value so
	// a diagnostic hiccup can never trigger a pointless retry.
	public static OverlayScreenState ReadScreenState(nint hwnd)
	{
		bool visible = IsWindowVisible(hwnd);
		bool cloaked = IsCloaked(hwnd);
		bool topmost = ((long)GetWindowLongPtr(hwnd, GwlExStyle) & WsExTopmost) != 0;

		bool? onCurrentDesktop = null;
		try
		{
			if (VirtualDesktops is { } manager && manager.IsWindowOnCurrentVirtualDesktop(hwnd, out int onCurrent) == 0)
			{
				onCurrentDesktop = onCurrent != 0;
			}
		}
		catch (COMException)
		{
			// Unknown — the verdict treats null as "could not say", not as a problem.
		}

		return new OverlayScreenState(visible, cloaked, onCurrentDesktop, topmost, FindCoveringWindow(hwnd));
	}

	// The first window ABOVE the overlay in the Z-order that a user would see over it: visible, uncloaked,
	// overlapping, and not itself a click-through see-through overlay (those are drawn on top of everything by
	// design — GPU/recording HUDs — and would otherwise read as "covering" on every check). Null when nothing
	// covers it. Bounded so a pathological Z-order can never hang the UI thread.
	private static string? FindCoveringWindow(nint hwnd)
	{
		if (!GetWindowRect(hwnd, out Rect overlay))
		{
			return null;
		}

		nint above = GetWindow(hwnd, GwHwndPrev);
		for (int guard = 0; above != nint.Zero && guard < 512; guard++, above = GetWindow(above, GwHwndPrev))
		{
			if (!IsWindowVisible(above) || IsCloaked(above) || !GetWindowRect(above, out Rect other))
			{
				continue;
			}

			long exStyle = (long)GetWindowLongPtr(above, GwlExStyle);
			bool seeThrough = (exStyle & (WsExLayered | WsExTransparent)) == (WsExLayered | WsExTransparent);
			bool overlaps = other.Left < overlay.Right && overlay.Left < other.Right
				&& other.Top < overlay.Bottom && overlay.Top < other.Bottom;
			if (seeThrough || !overlaps)
			{
				continue;
			}

			return Describe(above);
		}

		return null;
	}

	// "title [class] (process)" — enough to name the culprit in one log line.
	private static string Describe(nint hwnd)
	{
		StringBuilder title = new(256);
		_ = GetWindowText(hwnd, title, title.Capacity);
		StringBuilder className = new(256);
		_ = GetClassName(hwnd, className, className.Capacity);

		string process = "?";
		try
		{
			_ = GetWindowThreadProcessId(hwnd, out uint pid);
			using Process owner = Process.GetProcessById((int)pid);
			process = owner.ProcessName;
		}
		catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or Win32Exception)
		{
			// The owner exited between the Z-order walk and this lookup; the title and class still name it.
		}

		return $"\"{title}\" [{className}] ({process})";
	}

	private static bool IsCloaked(nint hwnd) =>
		DwmGetWindowAttribute(hwnd, DwmwaCloaked, out int cloaked, sizeof(int)) == 0 && cloaked != 0;

	// Created lazily on the (STA) UI thread; null on a system without virtual-desktop support.
	private static IVirtualDesktopManager? VirtualDesktops => _virtualDesktops ??= CreateVirtualDesktopManager();

	private static IVirtualDesktopManager? _virtualDesktops;

	private static IVirtualDesktopManager? CreateVirtualDesktopManager()
	{
		try
		{
			return (IVirtualDesktopManager)new VirtualDesktopManagerClass();
		}
		catch (COMException)
		{
			return null;
		}
	}

	private const long WsExTopmost = 0x00000008;
	private const uint SwpShowWindow = 0x0040;
	private const uint GwHwndPrev = 3;
	private const int DwmwaCloaked = 14;

	[StructLayout(LayoutKind.Sequential)]
	private struct Rect
	{
		public int Left;
		public int Top;
		public int Right;
		public int Bottom;
	}

	// The documented shell interface for virtual desktops (shobjidl_core.h).
	[ComImport]
	[Guid("a5cd92ff-29be-454c-8d04-d82879fb3f1b")]
	[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
	private interface IVirtualDesktopManager
	{
		[PreserveSig]
		int IsWindowOnCurrentVirtualDesktop(nint topLevelWindow, out int onCurrentDesktop);

		[PreserveSig]
		int GetWindowDesktopId(nint topLevelWindow, out Guid desktopId);

		[PreserveSig]
		int MoveWindowToDesktop(nint topLevelWindow, ref Guid desktopId);
	}

	[ComImport]
	[Guid("aa509086-5ca9-4c25-8f95-589d3c07b48a")]
	private class VirtualDesktopManagerClass
	{
	}

	[DllImport("user32.dll")]
	[return: MarshalAs(UnmanagedType.Bool)]
	private static extern bool IsWindowVisible(nint hWnd);

	[DllImport("user32.dll")]
	private static extern nint GetForegroundWindow();

	[DllImport("user32.dll")]
	private static extern nint GetWindow(nint hWnd, uint uCmd);

	[DllImport("user32.dll", SetLastError = true)]
	[return: MarshalAs(UnmanagedType.Bool)]
	private static extern bool GetWindowRect(nint hWnd, out Rect lpRect);

	[DllImport("user32.dll", CharSet = CharSet.Unicode)]
	private static extern int GetWindowText(nint hWnd, StringBuilder lpString, int nMaxCount);

	[DllImport("user32.dll", CharSet = CharSet.Unicode)]
	private static extern int GetClassName(nint hWnd, StringBuilder lpClassName, int nMaxCount);

	[DllImport("user32.dll")]
	private static extern uint GetWindowThreadProcessId(nint hWnd, out uint lpdwProcessId);

	[DllImport("dwmapi.dll")]
	private static extern int DwmGetWindowAttribute(nint hwnd, int dwAttribute, out int pvAttribute, int cbAttribute);

	[DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW", SetLastError = true)]
	private static extern nint GetWindowLongPtr(nint hWnd, int nIndex);

	[DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)]
	private static extern nint SetWindowLongPtr(nint hWnd, int nIndex, nint dwNewLong);

	[DllImport("user32.dll", SetLastError = true)]
	[return: MarshalAs(UnmanagedType.Bool)]
	private static extern bool SetWindowPos(nint hWnd, nint hWndInsertAfter, int x, int y, int cx, int cy, uint uFlags);
}
