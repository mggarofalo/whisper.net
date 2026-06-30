// Thin step definitions for the live-history feature. Each step delegates to the
// LiveHistoryDriver (injected by the Reqnroll DI plugin); no logic lives here.

using Dictation.Specs.Drivers;
using Reqnroll;

namespace Dictation.Specs.StepDefinitions;

[Binding]
public sealed class LiveHistorySteps(LiveHistoryDriver driver)
{
	[Given(@"the History section is open with no history yet")]
	public Task GivenOpenEmpty() => driver.OpenHistory();

	[Given(@"the History section is open showing ""(.*)"" and ""(.*)""")]
	public Task GivenOpenWith(string first, string second)
	{
		driver.HistoryAlreadyHas(first, second);
		return driver.OpenHistory();
	}

	[When(@"a transcription ""(.*)"" is recorded")]
	public Task WhenRecorded(string text) => driver.RecordDictation(text);

	[When(@"the user switches away from History and back")]
	public Task WhenSwitchAwayAndBack() => driver.SwitchAwayAndBack();

	[Given(@"the user navigates away from History")]
	public void GivenNavigatesAwayFromHistory() => driver.SwitchAway();

	[When(@"the user returns to History")]
	public void WhenReturnsToHistory() => driver.ReturnToHistory();

	[Then(@"the new transcription appears at the top of the history list")]
	public void ThenTopEntry() => driver.AssertTopIsLastRecorded();

	[Then(@"the history list is no longer empty")]
	public void ThenNotEmpty() => driver.AssertNotEmpty();

	[Then(@"the history list has (\d+) entries")]
	public void ThenEntryCount(int count) => driver.AssertEntryCount(count);

	[Then(@"the history list has (\d+) entry")]
	public void ThenEntryCountSingular(int count) => driver.AssertEntryCount(count);

	[Then(@"the history list still shows ""(.*)""")]
	public void ThenStillShows(string text) => driver.AssertListContains(text);
}
