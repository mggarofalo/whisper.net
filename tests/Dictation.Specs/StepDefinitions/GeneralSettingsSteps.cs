// Drives the start-at-login toggle scenarios. Steps stay thin; the GeneralSettingsDriver exercises the
// real GeneralViewModel over the Mediator pipeline, and the registration is seeded/asserted by the
// shared run-on-login steps (same scoped fake registration).

using Dictation.Specs.Drivers;
using Reqnroll;

namespace Dictation.Specs.StepDefinitions;

[Binding]
public sealed class GeneralSettingsSteps(GeneralSettingsDriver driver)
{
	[Given(@"the user opens the General settings section")]
	[When(@"the user opens the General settings section")]
	public void OpensTheGeneralSettingsSection() => driver.OpenSection();

	[When(@"the user turns the start-at-login toggle (.*)")]
	public void TurnsTheStartAtLoginToggle(string action) => driver.SetToggle(action == "on");

	[Then(@"the start-at-login toggle is (.*)")]
	public void TheStartAtLoginToggleIs(string toggle) => driver.AssertToggle(toggle == "on");
}
