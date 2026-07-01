// Where the dictation overlay sits on screen. The geometry is pure and WPF-free so it can
// be unit-tested: given the work area (the screen minus the taskbar) and the overlay's size, it returns
// the overlay's top-left so the overlay is horizontally centered and anchored a small margin above the
// bottom of the work area. The WPF overlay window resolves the actual work area (of the monitor holding
// the focused window, in DIPs) and applies this result; the on-screen placement across monitors and DPI
// scales is the manual-verification remainder.

using Application.Display;

namespace Logic.AppManagement;

/// <summary>A rectangle in device-independent pixels — a work area or screen bounds — kept WPF-free so the
/// overlay placement math is unit-testable. <see cref="Left"/>/<see cref="Top"/> may be negative or offset
/// (a secondary monitor to the left of, or above, the primary).</summary>
public readonly record struct OverlayRect(double Left, double Top, double Width, double Height)
{
	public double Right => Left + Width;

	public double Bottom => Top + Height;
}

public static class OverlayPlacement
{
	/// <summary>Default gap, in DIPs, between the overlay and the bottom of the work area.</summary>
	public const double DefaultBottomMargin = 24.0;

	/// <summary>
	/// Bottom-center placement: the overlay's top-left so it is horizontally centered in
	/// <paramref name="workArea"/> and anchored <paramref name="bottomMargin"/> DIPs above its bottom edge
	/// (above the taskbar). Honors the work area's origin, so a secondary-monitor offset carries through.
	/// </summary>
	public static (double Left, double Top) BottomCenter(
		OverlayRect workArea,
		double overlayWidth,
		double overlayHeight,
		double bottomMargin = DefaultBottomMargin)
	{
		double left = workArea.Left + ((workArea.Width - overlayWidth) / 2);
		double top = workArea.Bottom - overlayHeight - bottomMargin;
		return (left, top);
	}

	/// <summary>
	/// Choose the monitor the overlay should sit on. Prefers the monitor whose device name matches
	/// <paramref name="preferredDeviceName"/> (the persisted selection); falls back to the primary — and
	/// then to the first available — when the preference is null (the default "follow primary") or the chosen
	/// display is no longer attached, so a removed monitor self-heals to the primary rather than stranding the
	/// overlay off-screen. Returns null only when no monitors are available.
	/// </summary>
	public static MonitorInfo? ChooseMonitor(IReadOnlyList<MonitorInfo> monitors, string? preferredDeviceName)
	{
		if (monitors.Count == 0)
		{
			return null;
		}

		if (!string.IsNullOrEmpty(preferredDeviceName))
		{
			foreach (MonitorInfo monitor in monitors)
			{
				if (string.Equals(monitor.DeviceName, preferredDeviceName, StringComparison.OrdinalIgnoreCase))
				{
					return monitor;
				}
			}
		}

		foreach (MonitorInfo monitor in monitors)
		{
			if (monitor.IsPrimary)
			{
				return monitor;
			}
		}

		return monitors[0];
	}
}
