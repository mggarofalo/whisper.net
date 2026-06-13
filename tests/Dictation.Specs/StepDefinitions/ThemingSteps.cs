// Thin step definitions for the theming feature. Each step delegates to the ThemingDriver
// (injected by the Reqnroll DI plugin), which inspects the presentation artifacts directly.

using Dictation.Specs.Drivers;
using Reqnroll;

namespace Dictation.Specs.StepDefinitions;

[Binding]
public sealed class ThemingSteps(ThemingDriver driver)
{
	[Given(@"the presentation layer")]
	public void GivenThePresentationLayer()
	{
		// The presentation artifacts are inspected by the driver; nothing to set up.
	}

	[Then(@"the app applies the built-in Fluent theme following the system preference")]
	public void ThenTheAppAppliesTheFluentTheme() => driver.AssertAppOptsIntoSystemFluentTheme();

	[Then(@"the built-in-versus-library theming decision is recorded with rationale")]
	public void ThenTheDecisionIsRecorded() => driver.AssertThemingDecisionRecorded();
}
