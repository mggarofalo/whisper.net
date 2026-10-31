// Thin step definitions for the history browser feature. Each step delegates to the
// HistoryBrowserDriver (injected by the Reqnroll DI plugin); no logic lives here.

using Dictation.Specs.Drivers;
using Reqnroll;

namespace Dictation.Specs.StepDefinitions;

[Binding]
public sealed class HistoryBrowserSteps(HistoryBrowserDriver driver)
{
	[Given(@"transcriptions have been recorded previously")]
	public void GivenTranscriptionsExist() => driver.StoreHasThreeOutOfOrderEntries();

	[Given(@"the history view lists a past transcription")]
	public async Task GivenTheHistoryViewListsAnEntry()
	{
		driver.StoreHasThreeOutOfOrderEntries();
		await driver.OpenHistory();
	}

	[Given(@"more transcriptions exist than fit on one page")]
	public void GivenMoreThanOnePageExists() => driver.StoreHasEntriesAcrossTwoPages();

	[Given(@"exactly one full page of transcriptions exists")]
	public void GivenExactlyOneFullPageExists() => driver.StoreHasExactlyOneFullPage();

	[Given(@"no transcriptions have been recorded")]
	public void GivenNoTranscriptions() => driver.StoreIsEmpty();

	[When(@"the user opens the history view")]
	public async Task WhenTheUserOpensHistory() => await driver.OpenHistory();

	[When(@"the user browses to the next page")]
	public async Task WhenTheUserBrowsesNext() => await driver.BrowseToNextPage();

	[When(@"the user chooses to copy that entry")]
	public async Task WhenTheUserCopiesAnEntry() => await driver.CopyFirstEntry();

	[Given(@"these transcriptions have been recorded, oldest first:")]
	public void GivenTheseTranscriptions(DataTable table) => driver.StoreHasEntries(table.Rows.Select(row => row["text"]));

	[Given(@"(\d+) transcriptions mention ""(.*)"" among others that do not")]
	public void GivenTranscriptionsMention(int count, string term) => driver.StoreHasEntriesMentioning(count, term);

	[When(@"the user browses to the previous page")]
	public async Task WhenTheUserBrowsesPrevious() => await driver.BrowseToPreviousPage();

	[When(@"the user searches history for ""(.*)""")]
	public async Task WhenTheUserSearches(string text) => await driver.Search(text);

	[When(@"the user clears the history search")]
	public async Task WhenTheUserClearsTheSearch() => await driver.Search(string.Empty);

	[Then(@"the history view is on page (\d+) of (\d+)")]
	public void ThenOnPage(int page, int of) => driver.AssertOnPage(page, of);

	[Then(@"the newest transcription is listed first")]
	public void ThenNewestFirst() => driver.AssertNewestListedFirst();

	[Then(@"no previous history page is offered")]
	public void ThenNoPreviousPage() => driver.AssertNoPreviousPageOffered();

	[Then(@"only these transcriptions are listed:")]
	public void ThenOnlyTheseListed(DataTable table) => driver.AssertListed(table.Rows.Select(row => row["text"]));

	[Then(@"(\d+) transcriptions are listed")]
	public void ThenCountListed(int count) => driver.AssertListedCount(count);

	[Then(@"the search was answered by the history store")]
	public void ThenSearchAnsweredByStore() => driver.AssertSearchAnsweredByStore();

	[Then(@"the history view shows that nothing matches the search")]
	public void ThenNoMatches() => driver.AssertNoMatchesState();

	[Then(@"the history view does not show the empty-history state")]
	public void ThenNotEmptyHistory() => driver.AssertNotEmptyHistoryState();

	[Then(@"the recent transcriptions are listed most-recent-first")]
	public void ThenListedNewestFirst() => driver.AssertListedNewestFirst();

	[Then(@"the next page of transcriptions is shown")]
	public void ThenNextPageShown() => driver.AssertNextPageShown();

	[Then(@"a copy request is dispatched for that entry's text")]
	public void ThenCopyDispatched() => driver.AssertCopyDispatched();

	[Then(@"the history view shows an empty state")]
	public void ThenEmptyState() => driver.AssertEmptyState();

	[Then(@"a further history page is offered")]
	public void ThenFurtherPageOffered() => driver.AssertFurtherPageOffered();

	[Then(@"no further history pages are offered")]
	public void ThenNoFurtherPageOffered() => driver.AssertNoFurtherPageOffered();
}
