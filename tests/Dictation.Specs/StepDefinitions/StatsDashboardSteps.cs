// Thin step definitions for the stats dashboard feature. Each step delegates to the
// StatsDashboardDriver (injected by the Reqnroll DI plugin); no logic lives here.

using Dictation.Specs.Drivers;
using Reqnroll;

namespace Dictation.Specs.StepDefinitions;

[Binding]
public sealed class StatsDashboardSteps(StatsDashboardDriver driver)
{
	[Given(@"usage metrics have been recorded")]
	public void GivenUsageRecorded() => driver.StoreHasRecordedUsage();

	[Given(@"no usage metrics have been recorded")]
	public void GivenNoUsage() => driver.StoreIsEmpty();

	[When(@"the user opens the stats dashboard")]
	public async Task WhenTheUserOpensTheDashboard() => await driver.OpenDashboard();

	[When(@"more activity is recorded and the dashboard is refreshed")]
	public async Task WhenMoreActivityIsRecordedAndRefreshed()
	{
		driver.MoreUsageIsRecorded();
		await driver.RefreshDashboard();
	}

	[Then(@"the displayed totals match the recorded usage")]
	public void ThenTotalsMatchRecordedUsage() => driver.AssertTotals(transcriptions: 2, words: 5);

	[Then(@"the displayed totals include the new activity")]
	public void ThenTotalsIncludeNewActivity() => driver.AssertTotals(transcriptions: 3, words: 8);

	[Then(@"the stats display zeroed values without error")]
	public void ThenStatsAreZeroed() => driver.AssertZeroed();
}
