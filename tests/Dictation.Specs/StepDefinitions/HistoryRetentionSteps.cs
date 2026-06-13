// Drives the retention + paged-browsing scenarios. Steps stay thin; the
// HistoryRetentionDriver owns HOW the real Mediator pipeline and SQLite store are exercised against a
// private temp-file database.

using Dictation.Specs.Drivers;
using Reqnroll;

namespace Dictation.Specs.StepDefinitions;

[Binding]
public sealed class HistoryRetentionSteps(HistoryRetentionDriver driver)
{
	[Given(@"a retention limit of (\d+) entries")]
	public void GivenARetentionLimit(int maxEntries) => driver.SetRetentionLimit(maxEntries);

	[Given(@"the history already contains (\d+) entries")]
	public Task GivenTheHistoryAlreadyContains(int count) => driver.SeedEntries(count);

	[When(@"a new transcription is recorded")]
	public Task WhenANewTranscriptionIsRecorded() => driver.RecordNewTranscription();

	[When(@"I browse history with page size (-?\d+) and page (-?\d+)")]
	public Task WhenIBrowseHistory(int pageSize, int page) => driver.BrowsePage(pageSize, page);

	[Then(@"the history contains (\d+) entries")]
	public Task ThenTheHistoryContains(int expected) => driver.AssertHistoryCount(expected);

	[Then(@"the oldest prior entry has been removed")]
	public Task ThenTheOldestPriorEntryHasBeenRemoved() => driver.AssertOldestPriorEntryRemoved();

	[Then(@"I receive (\d+) history entries")]
	public void ThenIReceiveEntries(int expected) => driver.AssertReceivedCount(expected);

	[Then(@"they are ordered most-recent-first")]
	public void ThenTheyAreOrderedMostRecentFirst() => driver.AssertMostRecentFirst();

	[Then(@"the browse request is rejected")]
	public void ThenTheBrowseRequestIsRejected() => driver.AssertBrowseRejected();
}
