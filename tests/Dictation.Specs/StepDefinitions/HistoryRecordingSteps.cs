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
	HomeDashboardDriver home)
{
	[Given(@"the dictation history starts empty")]
	public void GivenTheDictationHistoryStartsEmpty() => driver.HistoryStartsEmpty();

	[Then(@"the history lists ""(.*)"" as the most recent entry")]
	public void ThenTheHistoryListsTheMostRecentEntry(string text) => browser.AssertMostRecentEntryIs(text);

	// The usage totals live on Home > Usage (WHISPER-148 removed the duplicate Stats page).
	[When(@"the user opens the Home dashboard")]
	public Task WhenTheUserOpensTheHomeDashboard() => home.OpenOverRecordedHistory();

	[Then(@"the Home usage totals count (\d+) transcriptions? and (\d+) words?")]
	public void ThenTheHomeUsageTotalsCount(int transcriptions, int words) =>
		home.AssertTotals(transcriptions, words);
}
