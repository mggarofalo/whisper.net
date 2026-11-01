// Thin step definitions for the version-and-updates feature (WHISPER-151). Each step delegates to the
// VersionAndUpdatesDriver, which drives the real GeneralViewModel over the Mediator pipeline.

using Dictation.Specs.Drivers;
using Reqnroll;

namespace Dictation.Specs.StepDefinitions;

[Binding]
public sealed class VersionAndUpdatesSteps(VersionAndUpdatesDriver driver)
{
	[Given(@"the running build's informational version is ""(.*)""")]
	public void GivenRunningBuild(string informationalVersion) => driver.RunningBuildIs(informationalVersion);

	[Given(@"the app is an installed build")]
	public void GivenInstalledBuild() => driver.InstalledBuild();

	[Given(@"the app is not an installed build")]
	public void GivenNotInstalledBuild() => driver.NotInstalledBuild();

	[Given(@"release ""(.*)"" is published")]
	public void GivenReleasePublished(string version) => driver.ReleasePublished(version);

	[Given(@"no newer release is published")]
	public void GivenNoNewerRelease() => driver.NoNewerRelease();

	[Given(@"the release feed cannot be reached")]
	public void GivenFeedUnreachable() => driver.FeedUnreachable();

	[Given(@"downloading the update fails")]
	public void GivenDownloadFails() => driver.DownloadFails();

	[Given(@"the user opens the General settings")]
	[When(@"the user opens the General settings")]
	public Task OpenSection() => driver.OpenSection();

	[Given(@"the user checks for updates")]
	[When(@"the user checks for updates")]
	public Task CheckForUpdates() => driver.CheckForUpdates();

	[When(@"the user chooses to download and restart")]
	public Task DownloadAndRestart() => driver.DownloadAndRestart();

	[When(@"the user turns on automatic update checks")]
	public void TurnOnAutomaticChecks() => driver.TurnOnAutomaticChecks();

	[Then(@"the version shown is ""(.*)""")]
	public void ThenVersionShown(string expected) => driver.AssertVersionShown(expected);

	[Then(@"no update check is made")]
	public Task ThenNoUpdateCheckMade() => driver.AssertNoUpdateCheckMade();

	[Then(@"the update status reads ""(.*)""")]
	public void ThenStatusReads(string expected) => driver.AssertStatus(expected);

	[Then(@"the user can download and restart")]
	public void ThenCanDownloadAndRestart() => driver.AssertCanDownloadAndRestart();

	[Then(@"the user cannot download and restart")]
	public void ThenCannotDownloadAndRestart() => driver.AssertCannotDownloadAndRestart();

	[Then(@"update ""(.*)"" is downloaded and staged to apply on restart")]
	public Task ThenDownloadedAndStaged(string version) => driver.AssertDownloadedAndStagedForRestart(version);

	[Then(@"the app shuts down so the update can be applied")]
	public void ThenShutsDown() => driver.AssertShutdownRequested();

	[Then(@"the app keeps running")]
	public void ThenKeepsRunning() => driver.AssertKeepsRunning();

	[Then(@"automatic update checks are off")]
	public void ThenAutomaticChecksOff() => driver.AssertAutomaticChecksOff();

	[Then(@"at startup no update check is made")]
	public Task ThenNoStartupCheck() => driver.AssertNoStartupCheck();

	[Then(@"automatic update checks are persisted as on")]
	public void ThenAutomaticChecksPersistedOn() => driver.AssertAutomaticChecksPersistedOn();

	[Then(@"at startup update ""(.*)"" is downloaded and staged to apply")]
	public Task ThenStartupCheckStages(string version) => driver.AssertStartupCheckStages(version);
}
