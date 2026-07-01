// Unit depth for the overlay placement geometry. Pins that the overlay is horizontally
// centered in the work area, anchored a small margin above its bottom edge, and that both carry through a
// work-area origin offset (a secondary monitor). The WPF window that resolves the work area and applies
// this is the manual-verification remainder (multi-monitor, DPI scales, taskbar positions).

using Application.Display;
using AwesomeAssertions;
using Logic.AppManagement;
using Xunit;

namespace Logic.AppManagement.Tests;

public sealed class OverlayPlacementTests
{
	private const double OverlayWidth = 208;
	private const double OverlayHeight = 44;

	private static readonly MonitorInfo Primary = new("\\\\.\\DISPLAY1", "Primary display", true, 0, 0, 1920, 1040);
	private static readonly MonitorInfo Secondary = new("\\\\.\\DISPLAY2", "Display 2", false, 1920, 0, 2560, 1400);

	[Fact]
	public void Centers_horizontally_in_the_work_area()
	{
		OverlayRect workArea = new(0, 0, 1920, 1040);

		(double left, _) = OverlayPlacement.BottomCenter(workArea, OverlayWidth, OverlayHeight);

		left.Should().BeApproximately((1920 - OverlayWidth) / 2, 0.001);

		// Symmetric: the gap to the left edge equals the gap to the right edge.
		double leftGap = left - workArea.Left;
		double rightGap = workArea.Right - (left + OverlayWidth);
		leftGap.Should().BeApproximately(rightGap, 0.001);
	}

	[Fact]
	public void Anchors_a_margin_above_the_bottom_of_the_work_area()
	{
		OverlayRect workArea = new(0, 0, 1920, 1040);

		(_, double top) = OverlayPlacement.BottomCenter(workArea, OverlayWidth, OverlayHeight, bottomMargin: 24);

		top.Should().BeApproximately(1040 - OverlayHeight - 24, 0.001);
		(top + OverlayHeight).Should().BeLessThan(workArea.Bottom, "the overlay sits above the work-area bottom (the taskbar)");
	}

	[Theory]
	[InlineData(0, 0, 1920, 1040)]       // primary
	[InlineData(1920, 0, 2560, 1400)]    // a monitor to the right
	[InlineData(-1920, 0, 1920, 1020)]   // a monitor to the left
	[InlineData(0, -1080, 1920, 1040)]   // a monitor above
	public void Honors_the_work_area_origin_and_stays_within_it(double x, double y, double w, double h)
	{
		OverlayRect workArea = new(x, y, w, h);

		(double left, double top) = OverlayPlacement.BottomCenter(workArea, OverlayWidth, OverlayHeight);

		left.Should().BeApproximately(x + ((w - OverlayWidth) / 2), 0.001);
		left.Should().BeGreaterThanOrEqualTo(workArea.Left);
		(left + OverlayWidth).Should().BeLessThanOrEqualTo(workArea.Right);
		(top + OverlayHeight).Should().BeLessThanOrEqualTo(workArea.Bottom);
		top.Should().BeGreaterThanOrEqualTo(workArea.Top, "an overlay smaller than the work area never clips its top");
	}

	[Fact]
	public void A_larger_margin_lifts_the_overlay_higher()
	{
		OverlayRect workArea = new(0, 0, 1920, 1040);

		(_, double small) = OverlayPlacement.BottomCenter(workArea, OverlayWidth, OverlayHeight, bottomMargin: 8);
		(_, double large) = OverlayPlacement.BottomCenter(workArea, OverlayWidth, OverlayHeight, bottomMargin: 64);

		large.Should().BeLessThan(small, "a larger bottom margin moves the overlay up");
	}

	[Fact]
	public void Chooses_the_monitor_whose_device_name_matches_the_preference()
	{
		MonitorInfo? chosen = OverlayPlacement.ChooseMonitor([Primary, Secondary], Secondary.DeviceName);

		chosen.Should().Be(Secondary, "the persisted display is honored when it is still attached");
	}

	[Fact]
	public void Falls_back_to_the_primary_when_no_preference_is_set()
	{
		MonitorInfo? chosen = OverlayPlacement.ChooseMonitor([Secondary, Primary], preferredDeviceName: null);

		chosen.Should().Be(Primary, "a null preference (the default) follows the primary display");
	}

	[Fact]
	public void Falls_back_to_the_primary_when_the_preferred_display_is_gone()
	{
		// The saved display has been unplugged: only the primary remains.
		MonitorInfo? chosen = OverlayPlacement.ChooseMonitor([Primary], Secondary.DeviceName);

		chosen.Should().Be(Primary, "a removed display self-heals to the primary rather than stranding the overlay");
	}

	[Fact]
	public void Falls_back_to_the_first_monitor_when_none_is_flagged_primary()
	{
		MonitorInfo first = Secondary with { IsPrimary = false };
		MonitorInfo second = Primary with { IsPrimary = false };

		MonitorInfo? chosen = OverlayPlacement.ChooseMonitor([first, second], preferredDeviceName: null);

		chosen.Should().Be(first, "with no primary flagged, the first available monitor is used");
	}

	[Fact]
	public void Returns_null_when_no_monitors_are_available()
	{
		MonitorInfo? chosen = OverlayPlacement.ChooseMonitor([], preferredDeviceName: null);

		chosen.Should().BeNull("an empty catalog leaves the caller to fall back to the primary work area");
	}
}
