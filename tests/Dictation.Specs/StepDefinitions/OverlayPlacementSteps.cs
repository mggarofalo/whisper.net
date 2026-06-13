// Thin step definitions for the overlay-placement feature. Each step delegates to the
// OverlayPlacementDriver (injected by the Reqnroll DI plugin); no logic lives here.

using Dictation.Specs.Drivers;
using Reqnroll;

namespace Dictation.Specs.StepDefinitions;

[Binding]
public sealed class OverlayPlacementSteps(OverlayPlacementDriver driver)
{
	[Given(@"a work area at (-?\d+),(-?\d+) sized (\d+) by (\d+)")]
	public void GivenWorkArea(double x, double y, double width, double height) => driver.SetWorkArea(x, y, width, height);

	[Given(@"the dictation overlay is (\d+) by (\d+)")]
	public void GivenOverlaySize(double width, double height) => driver.SetOverlaySize(width, height);

	[When(@"the overlay is positioned")]
	public void WhenPositioned() => driver.Place();

	[Then(@"the overlay is horizontally centered in the work area")]
	public void ThenCentered() => driver.AssertHorizontallyCentered();

	[Then(@"the overlay is anchored (\d+) above the bottom of the work area")]
	public void ThenAnchored(double margin) => driver.AssertAnchoredAboveBottom(margin);

	[Then(@"the overlay stays within the work area")]
	public void ThenWithin() => driver.AssertWithinWorkArea();
}
