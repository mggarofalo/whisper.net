// Drives the overlay-placement scenarios over the REAL, WPF-free OverlayPlacement geometry
// in Logic. It owns HOW placement is exercised so the steps stay one-liners: given a work area and the
// overlay size, it computes the bottom-center origin and asserts the overlay is horizontally centered,
// anchored a margin above the bottom, and within the work area. The WPF window that resolves the actual
// monitor work area and applies this is the manual-verification remainder.

using AwesomeAssertions;
using Logic.AppManagement;

namespace Dictation.Specs.Drivers;

public sealed class OverlayPlacementDriver
{
	private OverlayRect _workArea;
	private double _width;
	private double _height;
	private (double Left, double Top) _placed;

	public void SetWorkArea(double left, double top, double width, double height) =>
		_workArea = new OverlayRect(left, top, width, height);

	public void SetOverlaySize(double width, double height)
	{
		_width = width;
		_height = height;
	}

	public void Place() => _placed = OverlayPlacement.BottomCenter(_workArea, _width, _height);

	public void AssertHorizontallyCentered()
	{
		double leftGap = _placed.Left - _workArea.Left;
		double rightGap = _workArea.Right - (_placed.Left + _width);
		leftGap.Should().BeApproximately(rightGap, 0.001, "the overlay is centered horizontally in the work area");
	}

	public void AssertAnchoredAboveBottom(double margin)
	{
		_placed.Top.Should().BeApproximately(_workArea.Bottom - _height - margin, 0.001);
		(_placed.Top + _height).Should().BeLessThan(_workArea.Bottom, "the overlay stays above the work-area bottom (taskbar)");
	}

	public void AssertWithinWorkArea()
	{
		_placed.Left.Should().BeGreaterThanOrEqualTo(_workArea.Left);
		(_placed.Left + _width).Should().BeLessThanOrEqualTo(_workArea.Right);
		_placed.Top.Should().BeGreaterThanOrEqualTo(_workArea.Top);
		(_placed.Top + _height).Should().BeLessThanOrEqualTo(_workArea.Bottom);
	}
}
