// Thin step definitions for the history write-through scenarios. The recording driver
// arranges the round-tripping store; the existing browser/dashboard drivers own the real read path
// the assertions go through. No logic lives here.

using Dictation.Specs.Drivers;
using Reqnroll;

namespace Dictation.Specs.StepDefinitions;

[Binding]
public sealed class HistoryRecordingSteps(
	HistoryRecordingDriver driver,
	HistoryBrowserDriver browser,
	StatsDashboardDriver stats)
{
	[Given(@"the dictation history starts empty")]
	public void GivenTheDictationHistoryStartsEmpty() => driver.HistoryStartsEmpty();

	[Then(@"the history lists ""(.*)"" as the most recent entry")]
	public void ThenTheHistoryListsTheMostRecentEntry(string text) => browser.AssertMostRecentEntryIs(text);

	[Then(@"the usage stats count (\d+) transcriptions? and (\d+) words?")]
	public void ThenTheUsageStatsCount(int transcriptions, int words) =>
		stats.AssertTotals(transcriptions, words);
}
