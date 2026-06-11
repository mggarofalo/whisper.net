// Thin step definitions for the @WHISPER-106 Home dashboard feature. Each step delegates to the
// HomeDashboardDriver (injected by the Reqnroll DI plugin); no logic lives here.

using Dictation.Specs.Drivers;
using Reqnroll;

namespace Dictation.Specs.StepDefinitions;

[Binding]
public sealed class HomeDashboardSteps(HomeDashboardDriver driver)
{
	[Given(@"the dashboard's settings select model ""(.*)"" and input device ""(.*)""")]
	public void GivenSettings(string modelId, string deviceName) => driver.GivenSettings(modelId, deviceName);

	[Given(@"the dashboard's settings select model ""(.*)"" and the system default input device")]
	public void GivenSettingsSystemDefault(string modelId) => driver.GivenSettingsWithSystemDefaultDevice(modelId);

	[Given(@"two transcriptions totalling five words have been recorded")]
	public void GivenRecorded() => driver.GivenRecordedUsage();

	[Given(@"the dashboard has no recorded transcriptions")]
	public void GivenNoHistory() => driver.GivenNoHistory();

	[When(@"the Home section is opened")]
	public Task WhenOpened() => driver.OpenDashboard();

	[Then(@"the dashboard shows ""(.*)"" as the active model")]
	public void ThenModel(string modelId) => driver.AssertActiveModel(modelId);

	[Then(@"the dashboard shows ""(.*)"" as the input device")]
	public void ThenDevice(string deviceName) => driver.AssertInputDevice(deviceName);

	[Then(@"the dashboard shows the configured hotkey")]
	public void ThenHotkey() => driver.AssertShowsAHotkey();

	[Then(@"the dashboard shows (\d+) transcriptions and (\d+) words")]
	public void ThenTotals(int transcriptions, int words) => driver.AssertTotals(transcriptions, words);

	[Then(@"the dashboard lists (\d+) recent transcriptions")]
	public void ThenListsRecent(int count) => driver.AssertListsRecent(count);

	[Then(@"the dashboard shows zero usage totals")]
	public void ThenZero() => driver.AssertZeroTotals();

	[Then(@"the dashboard shows its empty recent state")]
	public void ThenEmpty() => driver.AssertEmptyRecent();
}
