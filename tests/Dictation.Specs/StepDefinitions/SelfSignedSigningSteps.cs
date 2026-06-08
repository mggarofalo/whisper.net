// Thin step definitions for the @WHISPER-72 self-signed code-signing feature. Each step delegates to the
// SelfSignedSigningDriver (injected by the Reqnroll DI plugin); no logic lives here. The "packaging
// configuration" Given is reused from PackagingSteps; this binding owns only the documentation Given.

using Dictation.Specs.Drivers;
using Reqnroll;

namespace Dictation.Specs.StepDefinitions;

[Binding]
public sealed class SelfSignedSigningSteps(SelfSignedSigningDriver driver)
{
	[Given(@"the repository documentation")]
	public void GivenTheRepositoryDocumentation()
	{
		// The documentation is read on demand from repository artifacts; nothing to set up.
	}

	[Then(@"a self-signed signing script emits a base64 PFX and password for pack\.ps1")]
	public void ThenScriptEmitsPfxAndPassword() => driver.AssertSigningScriptEmitsPfxAndPasswordForPackScript();

	[Then(@"the signing script commits no certificate or private key")]
	public void ThenScriptCommitsNoSecret() => driver.AssertSigningScriptCommitsNoCertificateOrKey();

	[Then(@"the signing script can trust the certificate in the local store for signtool verification")]
	public void ThenScriptCanTrustLocally() => driver.AssertSigningScriptCanTrustCertificateLocally();

	[Then(@"a build-and-run guide documents building from source, self-signed signing, and running")]
	public void ThenGuideDocumentsBuildSignRun() => driver.AssertBuildAndRunGuideDocumentsBuildSignAndRun();

	[Then(@"the build-and-run guide is honest that self-signed signing does not bypass SmartScreen")]
	public void ThenGuideHonestAboutSmartScreen() => driver.AssertBuildAndRunGuideIsHonestAboutSmartScreen();

	[Then(@"the README links to the build-and-run guide")]
	public void ThenReadmeLinksGuide() => driver.AssertReadmeLinksToBuildAndRunGuide();
}
