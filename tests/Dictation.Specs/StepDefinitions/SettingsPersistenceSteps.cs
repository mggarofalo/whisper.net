// @WHISPER-43 — drives the settings load-on-startup / persist-on-shutdown scenarios. Steps stay thin;
// the SettingsPersistenceDriver owns HOW the lifecycle service and the real file-backed store are
// exercised against a temp directory.

using Dictation.Specs.Drivers;
using Reqnroll;

namespace Dictation.Specs.StepDefinitions;

[Binding]
public sealed class SettingsPersistenceSteps(SettingsPersistenceDriver driver)
{
	[Given(@"the user has changed a setting and the application shuts down gracefully")]
	public Task GivenTheUserChangedASettingAndShutDown() => driver.ChangeASettingAndShutDownGracefully();

	[Given(@"no settings store exists")]
	public void GivenNoSettingsStoreExists() => driver.EnsureNoStoreExists();

	[Given(@"the settings store is corrupt")]
	public Task GivenTheSettingsStoreIsCorrupt() => driver.WriteCorruptStore();

	[When(@"the application starts")]
	[When(@"the application is started again")]
	public Task WhenTheApplicationStarts() => driver.StartApplication();

	[Then(@"the previously saved setting is loaded")]
	public void ThenThePreviouslySavedSettingIsLoaded() => driver.AssertChangedSettingLoaded();

	[Then(@"default settings are loaded")]
	public void ThenDefaultSettingsAreLoaded() => driver.AssertDefaultSettingsLoaded();

	[Then(@"a settings store is created")]
	public void ThenASettingsStoreIsCreated() => driver.AssertStoreCreated();

	[Then(@"the recovery is logged")]
	public void ThenTheRecoveryIsLogged() => driver.AssertRecoveryLogged();
}
