// Thin step definitions for the @WHISPER-45 history browser feature. Each step delegates to the
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

	[Given(@"no transcriptions have been recorded")]
	public void GivenNoTranscriptions() => driver.StoreIsEmpty();

	[When(@"the user opens the history view")]
	public async Task WhenTheUserOpensHistory() => await driver.OpenHistory();

	[When(@"the user browses to the next page")]
	public async Task WhenTheUserBrowsesNext() => await driver.BrowseToNextPage();

	[When(@"the user chooses to copy that entry")]
	public async Task WhenTheUserCopiesAnEntry() => await driver.CopyFirstEntry();

	[Then(@"the recent transcriptions are listed most-recent-first")]
	public void ThenListedNewestFirst() => driver.AssertListedNewestFirst();

	[Then(@"the next page of transcriptions is shown")]
	public void ThenNextPageShown() => driver.AssertNextPageShown();

	[Then(@"a copy request is dispatched for that entry's text")]
	public void ThenCopyDispatched() => driver.AssertCopyDispatched();

	[Then(@"the history view shows an empty state")]
	public void ThenEmptyState() => driver.AssertEmptyState();
}
