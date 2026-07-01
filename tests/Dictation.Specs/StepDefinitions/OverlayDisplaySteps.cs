// Drives the overlay-display picker scenarios. Steps stay thin; the OverlayDisplayDriver exercises the
// real GeneralViewModel over the Mediator pipeline (GetSettings / ListMonitors / UpdateSettings) with a
// round-tripping store and the substituted monitor catalog.

using Dictation.Specs.Drivers;
using Reqnroll;

namespace Dictation.Specs.StepDefinitions;

[Binding]
public sealed class OverlayDisplaySteps(OverlayDisplayDriver driver)
{
	[Given("a second display is attached")]
	public void ASecondDisplayIsAttached() => driver.GivenASecondDisplayIsAttached();

	[Given("the user opens the overlay display settings")]
	[When("the user opens the overlay display settings")]
	public Task OpensTheOverlayDisplaySettings() => driver.OpenSection();

	[Then("the primary display is offered as the default")]
	public void ThePrimaryDisplayIsOfferedAsTheDefault() => driver.AssertPrimaryDefaultOffered();

	[Then("the overlay display selection follows the primary by default")]
	public void TheOverlaySelectionFollowsThePrimary() => driver.AssertSelectionFollowsPrimary();

	[Then("the second display is listed as a choice")]
	public void TheSecondDisplayIsListed() => driver.AssertSecondDisplayListed();

	[When("the user selects the second display")]
	public void TheUserSelectsTheSecondDisplay() => driver.SelectSecondDisplay();

	[Then("the overlay display is persisted as the second display")]
	public void TheOverlayDisplayIsPersistedAsTheSecondDisplay() => driver.AssertPersistedIsSecondDisplay();

	[Then("reopening the section still shows the second display selected")]
	public Task ReopeningStillShowsTheSecondDisplay() => driver.AssertReopeningShowsSecondDisplay();
}
