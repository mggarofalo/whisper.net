// Thin step definitions for the signed auto-update feature. Each step delegates to the
// AutoUpdateDriver (injected by the Reqnroll DI plugin); no logic lives here. The "packaging
// configuration" given is shared with the packaging steps.

using Dictation.Specs.Drivers;
using Reqnroll;

namespace Dictation.Specs.StepDefinitions;

[Binding]
public sealed class AutoUpdateSteps(AutoUpdateDriver driver)
{
	[Given(@"automatic updates are enabled")]
	public void GivenUpdatesEnabled() => driver.AutomaticUpdatesEnabled();

	[Given(@"automatic updates are disabled")]
	public void GivenUpdatesDisabled() => driver.AutomaticUpdatesDisabled();

	[Given(@"a newer release ""(.*)"" is available on the channel")]
	public void GivenNewerReleaseAvailable(string version) => driver.NewerReleaseAvailable(version);

	[Given(@"the app is already up to date")]
	public void GivenUpToDate() => driver.AlreadyUpToDate();

	[Given(@"the update channel is unreachable")]
	public void GivenChannelUnreachable() => driver.ChannelUnreachable();

	[When(@"the app checks for updates")]
	public async Task WhenChecksForUpdates() => await driver.CheckForUpdates();

	[Then(@"the update ""(.*)"" is downloaded and staged to apply")]
	public async Task ThenUpdateDownloadedAndStaged(string version) => await driver.AssertUpdateDownloadedAndStaged(version);

	[Then(@"the app continues on the current version")]
	public void ThenContinuesOnCurrentVersion() => driver.AssertContinuesOnCurrentVersion();

	[Then(@"the failure is logged")]
	public void ThenFailureLogged() => driver.AssertFailureLogged();

	[Then(@"no update is applied")]
	public async Task ThenNoUpdateApplied() => await driver.AssertNoUpdateApplied();

	[Then(@"no update check is performed")]
	public async Task ThenNoUpdateCheckPerformed() => await driver.AssertNoUpdateCheckPerformed();

	[Then(@"the installer is code-signed when a signing certificate is supplied from a secret")]
	public void ThenInstallerSignedWhenCertProvided() => driver.AssertInstallerSignedWhenCertificateProvided();
}
