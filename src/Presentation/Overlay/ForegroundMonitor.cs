// Resolves the work area (in DIPs) of the monitor that holds the foreground window, for WHISPER-100
// overlay placement. Win32 monitor geometry is in physical pixels, so it is converted to DIPs with the
// overlay's current DPI scale. Any failure returns null so the caller falls back to the primary monitor's
// work area. Robust placement across mixed-DPI multi-monitor setups is the manual-verification remainder.

using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;
using Logic.AppManagement;

namespace Presentation.Overlay;

internal static class ForegroundMonitor
{
	private const uint MonitorDefaultToNearest = 0x00000002;

	public static OverlayRect? WorkArea(Visual overlay)
	{
		try
		{
			IntPtr foreground = GetForegroundWindow();
			if (foreground == IntPtr.Zero)
			{
				return null;
			}

			IntPtr monitor = MonitorFromWindow(foreground, MonitorDefaultToNearest);
			if (monitor == IntPtr.Zero)
			{
				return null;
			}

			MonitorInfo info = new() { Size = Marshal.SizeOf<MonitorInfo>() };
			if (!GetMonitorInfo(monitor, ref info))
			{
				return null;
			}

			// Physical-pixel work area -> DIPs. Using the overlay's DPI is exact on the common single-DPI
			// case; cross-monitor mixed-DPI placement is the manual remainder.
			DpiScale dpi = VisualTreeHelper.GetDpi(overlay);
			double scaleX = dpi.DpiScaleX > 0 ? dpi.DpiScaleX : 1.0;
			double scaleY = dpi.DpiScaleY > 0 ? dpi.DpiScaleY : 1.0;

			NativeRect work = info.WorkArea;
			return new OverlayRect(
				work.Left / scaleX,
				work.Top / scaleY,
				(work.Right - work.Left) / scaleX,
				(work.Bottom - work.Top) / scaleY);
		}
		catch (Exception)
		{
			return null;
		}
	}

	[DllImport("user32.dll")]
	private static extern IntPtr GetForegroundWindow();

	[DllImport("user32.dll")]
	private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint flags);

	[DllImport("user32.dll")]
	[return: MarshalAs(UnmanagedType.Bool)]
	private static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfo info);

	[StructLayout(LayoutKind.Sequential)]
	private struct NativeRect
	{
		public int Left;
		public int Top;
		public int Right;
		public int Bottom;
	}

	[StructLayout(LayoutKind.Sequential)]
	private struct MonitorInfo
	{
		public int Size;
		public NativeRect Monitor;
		public NativeRect WorkArea;
		public uint Flags;
	}
}
