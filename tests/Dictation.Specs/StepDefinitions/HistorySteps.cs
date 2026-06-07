// Thin step definitions for the history feature. Each step delegates to the HistoryDriver (injected by
// the Reqnroll DI plugin); no logic lives here.

using Dictation.Specs.Drivers;
using Reqnroll;

namespace Dictation.Specs.StepDefinitions;

[Binding]
public sealed class HistorySteps(HistoryDriver driver)
{
	private string _text = string.Empty;

	[Given(@"a completed transcription with text ""(.*)""")]
	public void GivenACompletedTranscription(string text) => _text = text;

	[When("the transcription is recorded")]
	public Task WhenTheTranscriptionIsRecorded() => driver.RecordTranscription(_text);

	[Then("a matching transcript entry is saved in the history store")]
	public void ThenAMatchingEntryIsSaved() => driver.AssertEntrySaved(_text);

	[Given("the history store contains three transcript entries from different times")]
	public void GivenThreeEntries() => driver.StoreHasThreeEntriesFromDifferentTimes();

	[When("the history is queried with a limit of two")]
	public Task WhenQueriedWithLimitTwo() => driver.QueryHistory(2);

	[Then("the two most recent entries are returned newest-first")]
	public void ThenTwoMostRecentNewestFirst() => driver.AssertTwoMostRecentNewestFirst();
}
