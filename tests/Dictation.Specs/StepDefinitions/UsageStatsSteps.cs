// Thin step definitions for the usage-statistics feature. Each step delegates to the UsageStatsDriver
// (injected by the Reqnroll DI plugin); no logic lives here.

using Dictation.Specs.Drivers;
using Reqnroll;

namespace Dictation.Specs.StepDefinitions;

[Binding]
public sealed class UsageStatsSteps(UsageStatsDriver driver)
{
	[Given(@"the history store contains transcriptions totaling (\d+) words across (\d+) sessions")]
	public void GivenTheStoreContains(int words, int sessions) => driver.StoreContains(words, sessions);

	[Given("the history store is empty")]
	public void GivenTheStoreIsEmpty() => driver.StoreIsEmpty();

	[When("usage statistics are requested")]
	public Task WhenUsageStatisticsAreRequested() => driver.RequestUsageStats();

	[Then(@"the returned usage stats report (\d+) words and (\d+) sessions")]
	public void ThenTheStatsReport(int words, int sessions) => driver.AssertReports(words, sessions);
}
