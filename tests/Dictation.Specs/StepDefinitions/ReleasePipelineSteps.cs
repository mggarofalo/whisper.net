// Thin step definitions for the @WHISPER-39 tag-driven release pipeline feature. Each step delegates to
// the ReleaseWorkflowDriver (injected by the Reqnroll DI plugin); no logic lives here.

using Dictation.Specs.Drivers;
using Reqnroll;

namespace Dictation.Specs.StepDefinitions;

[Binding]
public sealed class ReleasePipelineSteps(ReleaseWorkflowDriver driver)
{
	[Given(@"the release workflow")]
	public void GivenTheReleaseWorkflow()
	{
		// The workflow is read on demand from the repository; nothing to set up.
	}

	[Then(@"it triggers only on tags matching a version pattern")]
	public void ThenTriggersOnVersionTags() => driver.AssertTriggersOnlyOnVersionTags();

	[Then(@"it does not run on pull requests or branch pushes")]
	public void ThenNotOnPrOrBranches() => driver.AssertDoesNotRunOnPullRequestsOrBranches();

	[Then(@"it builds with warnings-as-errors and runs the tests")]
	public void ThenBuildsWarnAsErrorsAndTests() => driver.AssertBuildsWarningsAsErrorsAndTests();

	[Then(@"it builds and tests before it packages or publishes")]
	public void ThenBuildsAndTestsFirst() => driver.AssertBuildsAndTestsBeforePackagingOrPublishing();

	[Then(@"no build or test step is allowed to continue on error")]
	public void ThenNoContinueOnError() => driver.AssertNoBuildOrTestStepContinuesOnError();

	[Then(@"the version is derived from the tag by MinVer")]
	public void ThenVersionFromTag() => driver.AssertVersionDerivedFromTagByMinVer();

	[Then(@"it packages the installer with Velopack")]
	public void ThenPackagesWithVelopack() => driver.AssertPackagesWithVelopack();

	[Then(@"it publishes the installer and update package to a GitHub Release")]
	public void ThenPublishesToRelease() => driver.AssertPublishesInstallerAndUpdatePackageToARelease();

	[Then(@"code-signing secrets are injected from GitHub Actions secrets")]
	public void ThenSecretsInjected() => driver.AssertSigningSecretsInjectedFromActionsSecrets();

	[Then(@"no secret is echoed to the log")]
	public void ThenNoSecretEchoed() => driver.AssertNoSecretIsEchoed();
}
