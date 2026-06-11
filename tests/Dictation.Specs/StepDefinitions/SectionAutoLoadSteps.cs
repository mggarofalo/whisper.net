// Thin step definitions for the @WHISPER-108 section auto-load feature. Each step delegates to the
// SectionAutoLoadDriver (injected by the Reqnroll DI plugin); no logic lives here.

using Dictation.Specs.Drivers;
using Reqnroll;

namespace Dictation.Specs.StepDefinitions;

[Binding]
public sealed class SectionAutoLoadSteps(SectionAutoLoadDriver driver)
{
	[Given(@"capture devices ""(.*)"" and ""(.*)"" are available")]
	public void GivenCaptureDevicesAreAvailable(string first, string second) => driver.DevicesAvailable(first, second);

	[Given(@"the history store holds a transcription ""(.*)""")]
	public void GivenTheHistoryStoreHoldsATranscription(string text) => driver.HistoryHolds(text);

	[Given(@"the history store holds two recorded transcriptions")]
	public void GivenTheHistoryStoreHoldsTwoRecordedTranscriptions() => driver.HistoryHoldsTwoTranscriptions();

	[Given(@"the user has opened the ""(.*)"" section")]
	public async Task GivenTheUserHasOpenedTheSection(string section) => await driver.OpenSection(section);

	[Given(@"the history load will not complete until released")]
	public void GivenTheHistoryLoadWillNotComplete() => driver.HistoryLoadHangsUntilReleased();

	[When(@"the user opens the ""(.*)"" section")]
	public async Task WhenTheUserOpensTheSection(string section) => await driver.OpenSection(section);

	[When(@"the user rapidly switches away and back to the ""(.*)"" section twice")]
	public async Task WhenTheUserRapidlySwitchesAwayAndBack(string section) => await driver.SwitchAwayAndBackTwice(section);

	[When(@"the user refreshes the history section")]
	public async Task WhenTheUserRefreshesTheHistorySection() => await driver.RefreshHistory();

	[When(@"the user opens the ""(.*)"" section while the load is pending")]
	public void WhenTheUserOpensTheSectionWhileTheLoadIsPending(string section) => driver.OpenSectionWhileLoadPending(section);

	[When(@"a duplicate load is attempted the way the view invokes it")]
	public void WhenADuplicateLoadIsAttempted() => driver.AttemptDuplicateLoadLikeTheView();

	[When(@"the pending history load completes")]
	public async Task WhenThePendingHistoryLoadCompletes() => await driver.ReleasePendingLoad();

	[Then(@"the model list is populated without a manual refresh")]
	public void ThenTheModelListIsPopulated() => driver.AssertModelListPopulated();

	[Then(@"the device picker lists ""(.*)"" and ""(.*)"" without a manual refresh")]
	public void ThenTheDevicePickerLists(string first, string second) => driver.AssertDevicesListed(first, second);

	[Then(@"the history list shows ""(.*)"" without a manual refresh")]
	public void ThenTheHistoryListShows(string text) => driver.AssertHistoryShows(text);

	[Then(@"the dashboard shows the recorded totals without a manual refresh")]
	public void ThenTheDashboardShowsTheRecordedTotals() => driver.AssertRecordedTotalsShown();

	[Then(@"the history was queried exactly once")]
	public void ThenTheHistoryWasQueriedExactlyOnce() => driver.AssertHistoryQueriedExactly(1);

	[Then(@"the history was queried exactly twice")]
	public void ThenTheHistoryWasQueriedExactlyTwice() => driver.AssertHistoryQueriedExactly(2);

	[Then(@"the duplicate attempt was refused")]
	public void ThenTheDuplicateAttemptWasRefused() => driver.AssertDuplicateAttemptRefused();
}
