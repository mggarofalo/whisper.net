// The real IMonitorCatalog: enumerates attached monitors via the Win32 display APIs and projects each to
// a framework-neutral MonitorInfo. Work areas come back from GetMonitorInfo in physical pixels; the app is
// System-DPI-aware (no dpiAwareness manifest), so WPF's window coordinate space is a single uniform DIP
// grid (physical / system-DPI) across the whole virtual desktop, with Windows bitmap-scaling secondary
// monitors. Converting each physical work area by the system scale therefore yields coordinates the
// overlay can hand straight to Window.Left/Top and land on-screen on any monitor. Deliberately defensive:
// any failure yields an empty list, and the overlay/picker fall back to the primary. This is the single
// place that reads the display topology; the placement math itself is the WPF-free OverlayPlacement.

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Application.Display;
using Application.Ports;
using Microsoft.Extensions.Logging;

namespace Presentation.Overlay;

public sealed class Win32MonitorCatalog(ILogger<Win32MonitorCatalog> logger) : IMonitorCatalog
{
	private const int MonitorInfoFPrimary = 0x1;
	private const int CchDevice = 32;

	public IReadOnlyList<MonitorInfo> GetMonitors()
	{
		try
		{
			double scale = SystemScale();
			List<MonitorInfo> monitors = [];
			int index = 0;

			bool Collect(nint hMonitor, nint hdc, nint rect, nint data)
			{
				MonitorInfoEx info = new() { CbSize = Marshal.SizeOf<MonitorInfoEx>() };
				if (GetMonitorInfoW(hMonitor, ref info))
				{
					index++;
					bool isPrimary = (info.Flags & MonitorInfoFPrimary) != 0;
					int widthPx = info.Monitor.Right - info.Monitor.Left;
					int heightPx = info.Monitor.Bottom - info.Monitor.Top;

					monitors.Add(new MonitorInfo(
						DeviceName: info.Device,
						FriendlyName: $"{(isPrimary ? "Primary display" : $"Display {index}")} ({widthPx} × {heightPx})",
						IsPrimary: isPrimary,
						WorkAreaLeft: info.Work.Left / scale,
						WorkAreaTop: info.Work.Top / scale,
						WorkAreaWidth: (info.Work.Right - info.Work.Left) / scale,
						WorkAreaHeight: (info.Work.Bottom - info.Work.Top) / scale));
				}

				return true;
			}

			if (!EnumDisplayMonitors(nint.Zero, nint.Zero, Collect, nint.Zero))
			{
				logger.LogWarning("EnumDisplayMonitors reported failure; overlay will fall back to the primary work area.");
			}

			// Primary first so the picker and the fallback agree on the default display.
			monitors.Sort((a, b) => b.IsPrimary.CompareTo(a.IsPrimary));
			return monitors;
		}
		catch (Exception ex)
		{
			logger.LogWarning(ex, "Could not enumerate monitors; overlay will fall back to the primary work area.");
			return [];
		}
	}

	// The system DPI scale (1.0 at 96 DPI). Under System-DPI awareness this is the single scale WPF uses for
	// window coordinates across every monitor. Defensive: an unavailable API resolves to 1.0.
	private static double SystemScale()
	{
		try
		{
			uint dpi = GetDpiForSystem();
			return dpi == 0 ? 1.0 : dpi / 96.0;
		}
		catch (EntryPointNotFoundException)
		{
			return 1.0;
		}
	}

	[StructLayout(LayoutKind.Sequential)]
	private struct Rect
	{
		public int Left;
		public int Top;
		public int Right;
		public int Bottom;
	}

	[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
	private struct MonitorInfoEx
	{
		public int CbSize;
		public Rect Monitor;
		public Rect Work;
		public uint Flags;

		[MarshalAs(UnmanagedType.ByValTStr, SizeConst = CchDevice)]
		public string Device;
	}

	private delegate bool MonitorEnumProc(nint hMonitor, nint hdcMonitor, nint lprcMonitor, nint dwData);

	[DllImport("user32.dll")]
	[return: MarshalAs(UnmanagedType.Bool)]
	private static extern bool EnumDisplayMonitors(nint hdc, nint lprcClip, MonitorEnumProc lpfnEnum, nint dwData);

	[DllImport("user32.dll", CharSet = CharSet.Unicode)]
	[return: MarshalAs(UnmanagedType.Bool)]
	private static extern bool GetMonitorInfoW(nint hMonitor, ref MonitorInfoEx lpmi);

	[DllImport("user32.dll")]
	private static extern uint GetDpiForSystem();
}
