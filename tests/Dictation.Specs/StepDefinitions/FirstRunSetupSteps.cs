// Thin step definitions for the @WHISPER-82 first-run-setup feature. Each step delegates to the
// SetupStatusDriver (injected by the Reqnroll DI plugin); no logic lives here.

using Dictation.Specs.Drivers;
using Reqnroll;

namespace Dictation.Specs.StepDefinitions;

[Binding]
public sealed class FirstRunSetupSteps(SetupStatusDriver driver)
{
	[Given(@"setup was completed")]
	public void GivenSetupWasCompleted() => driver.SetupWasCompleted();

	[Given(@"the model ""(.*)"" is downloaded")]
	public void GivenTheModelIsDownloaded(string id) => driver.ModelIsDownloaded(id);

	[Given(@"the model ""(.*)"" is not downloaded")]
	public void GivenTheModelIsNotDownloaded(string id) => driver.ModelIsNotDownloaded(id);

	[When(@"the user activates the model ""(.*)""")]
	public Task WhenTheUserActivatesTheModel(string id) => driver.ActivateModel(id);

	[When(@"the launch setup check runs")]
	public Task WhenTheLaunchSetupCheckRuns() => driver.CheckSetup();

	[Then(@"the app is configured")]
	public void ThenTheAppIsConfigured() => driver.AssertConfigured();

	[Then(@"the app is not configured")]
	public void ThenTheAppIsNotConfigured() => driver.AssertNotConfigured();
}
