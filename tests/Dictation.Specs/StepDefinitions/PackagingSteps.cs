// Thin step definitions for the @WHISPER-20 self-contained installer packaging feature. Each step
// delegates to the PackagingDriver (injected by the Reqnroll DI plugin); no logic lives here.

using Dictation.Specs.Drivers;
using Reqnroll;

namespace Dictation.Specs.StepDefinitions;

[Binding]
public sealed class PackagingSteps(PackagingDriver driver)
{
	[Given(@"the packaging configuration")]
	public void GivenThePackagingConfiguration()
	{
		// The configuration is read on demand from repository artifacts; nothing to set up.
	}

	[Then(@"the Presentation project publishes self-contained for win-x64 as a single file")]
	public void ThenPublishesSelfContainedSingleFile() => driver.AssertPublishesSelfContainedSingleFileWinX64();

	[Then(@"the native assets are kept loose for the runtime loader to find")]
	public void ThenNativeAssetsLoose() => driver.AssertNativeAssetsLooseForTheLoader();

	[Then(@"no static assembly version is committed")]
	public void ThenNoStaticVersion() => driver.AssertNoStaticAssemblyVersionCommitted();

	[Then(@"the version is derived from git tags by MinVer")]
	public void ThenVersionFromMinVer() => driver.AssertVersionDerivedFromMinVer();

	[Then(@"the packaging script reads the version from MinVer rather than a literal")]
	public void ThenScriptReadsVersionFromMinVer() => driver.AssertPackScriptReadsVersionFromMinVer();

	[Then(@"a one-command packaging script builds a Velopack installer")]
	public void ThenOneCommandScript() => driver.AssertOneCommandScriptBuildsVelopackInstaller();

	[Then(@"the app id and icon are set")]
	public void ThenAppIdAndIcon() => driver.AssertAppIdAndIconAreSet();

	[Then(@"the vpk tool is pinned for a reproducible build")]
	public void ThenVpkPinned() => driver.AssertVpkToolIsPinned();
}
