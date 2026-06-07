// @WHISPER-32 — drives the run-on-login scenarios. Steps stay thin; the RunOnLoginDriver sends the
// real SetRunOnLoginCommand / GetRunOnLoginQuery through IMediator over a fake startup registration.

using Dictation.Specs.Drivers;
using Reqnroll;

namespace Dictation.Specs.StepDefinitions;

[Binding]
public sealed class RunOnLoginSteps(RunOnLoginDriver driver)
{
	[Given(@"run-on-login is currently (.*)")]
	public void GivenRunOnLoginIsCurrently(string initial) => driver.GivenCurrentlyEnabled(initial == "enabled");

	[When(@"the user sets run-on-login to (.*)")]
	public Task WhenTheUserSetsRunOnLoginTo(string target) => driver.SetEnabled(target == "enabled");

	[Then(@"the startup registration is (.*)")]
	public Task ThenTheStartupRegistrationIs(string expected) => driver.AssertRegistration(expected == "present");
}
