// Thin step definitions for the @WHISPER-51 first-run onboarding feature. Each step delegates to the
// OnboardingDriver (injected by the Reqnroll DI plugin); no logic lives here.

using Dictation.Specs.Drivers;
using Reqnroll;

namespace Dictation.Specs.StepDefinitions;

[Binding]
public sealed class OnboardingSteps(OnboardingDriver driver)
{
	[Given(@"the application has no completed setup")]
	[Given(@"onboarding offers to download a model")]
	public void GivenNoCompletedSetup() => driver.NoCompletedSetup();

	[Given(@"the user has completed the onboarding steps")]
	public async Task GivenTheUserCompletedOnboarding() => await driver.UserCompletedOnboarding();

	[Given(@"the required permissions are denied at first")]
	public void GivenPermissionsDeniedAtFirst() => driver.PermissionsDeniedThenGranted();

	[When(@"onboarding is evaluated at startup")]
	[When(@"onboarding is evaluated after a restart")]
	public async Task WhenOnboardingIsEvaluated() => await driver.ApplicationStarts();

	[When(@"the user picks a model, an input device, and a hotkey")]
	public async Task WhenTheUserPicksSetup() => await driver.RunGuidedSteps();

	[When(@"the user does not approve the download")]
	public void WhenTheUserDeclinesDownload() => driver.DeclineOfferedDownload();

	[When(@"the user requests permissions and then re-attempts")]
	public void WhenTheUserRequestsAndReattempts() => driver.RequestPermissionsThenReattempt();

	[Then(@"the onboarding flow is shown")]
	public void ThenOnboardingShown() => driver.AssertOnboardingShown();

	[Then(@"the onboarding flow is not shown again")]
	public void ThenOnboardingNotShown() => driver.AssertOnboardingNotShown();

	[Then(@"the chosen setup is applied through the mediator")]
	public void ThenChosenSetupApplied() => driver.AssertChosenSetupApplied();

	[Then(@"no model download occurs")]
	public void ThenNoModelDownloaded() => driver.AssertNoModelDownloaded();

	[Then(@"the permissions are reported as granted")]
	public void ThenPermissionsGranted() => driver.AssertPermissionsGranted();
}
