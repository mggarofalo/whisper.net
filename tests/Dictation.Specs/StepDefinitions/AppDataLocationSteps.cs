// Thin step definitions for the data-location feature. Each step delegates to the
// AppDataLocationDriver (injected by the Reqnroll DI plugin); no logic lives here.

using Dictation.Specs.Drivers;
using Reqnroll;

namespace Dictation.Specs.StepDefinitions;

[Binding]
public sealed class AppDataLocationSteps(AppDataLocationDriver driver)
{
	[When(@"the application resolves its per-user data directories")]
	public void WhenApplicationResolvesDataDirectories() => driver.ResolveDataDirectories();

	[Then(@"the data-root folder name is not the Velopack pack id")]
	public void ThenDataRootIsNotThePackId() => driver.AssertDataRootIsNotThePackId();

	[Then(@"the logs directory is outside the Velopack install root")]
	public void ThenLogsOutsideInstallRoot() => driver.AssertLogsOutsideInstallRoot();

	[Then(@"the model cache directory is outside the Velopack install root")]
	public void ThenModelCacheOutsideInstallRoot() => driver.AssertModelCacheOutsideInstallRoot();

	[Then(@"the settings database is outside the Velopack install root")]
	public void ThenDatabaseOutsideInstallRoot() => driver.AssertDatabaseOutsideInstallRoot();
}
