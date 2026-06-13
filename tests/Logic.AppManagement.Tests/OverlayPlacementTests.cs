// Unit depth for the overlay placement geometry. Pins that the overlay is horizontally
// centered in the work area, anchored a small margin above its bottom edge, and that both carry through a
// work-area origin offset (a secondary monitor). The WPF window that resolves the work area and applies
// this is the manual-verification remainder (multi-monitor, DPI scales, taskbar positions).

using AwesomeAssertions;
using Logic.AppManagement;
using Xunit;

namespace Logic.AppManagement.Tests;

public sealed class OverlayPlacementTests
{
	private const double OverlayWidth = 208;
	private const double OverlayHeight = 44;

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
}
