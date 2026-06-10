// Thin step definitions for the @WHISPER-91 collection-synchronization feature. Each step delegates to
// the CollectionSyncDriver (injected by the Reqnroll DI plugin); no logic lives here.

using Dictation.Specs.Drivers;
using Reqnroll;

namespace Dictation.Specs.StepDefinitions;

[Binding]
public sealed class CollectionSyncSteps(CollectionSyncDriver driver)
{
	[Given(@"a history view-model built over the collection-sync seam")]
	public void GivenAHistoryViewModelBuiltOverTheCollectionSyncSeam() => driver.CreateHistoryViewModel();

	[Then(@"its entries collection is registered together with its lock")]
	public void ThenItsEntriesCollectionIsRegisteredTogetherWithItsLock() => driver.AssertEntriesRegisteredWithGate();

	[Given(@"a synchronized bindable collection")]
	public void GivenASynchronizedBindableCollection() => driver.CreateStandaloneCollection();

	[Given(@"another thread is holding the collection's lock")]
	public void GivenAnotherThreadIsHoldingTheCollectionsLock() => driver.HoldGateOnAnotherThread();

	[When(@"an item is added on a background thread")]
	public void WhenAnItemIsAddedOnABackgroundThread() => driver.StartAddOnBackgroundThread();

	[Then(@"the add completes only after the lock is released")]
	public void ThenTheAddCompletesOnlyAfterTheLockIsReleased() => driver.AssertAddCompletesOnlyAfterGateReleased();

	[Given(@"a history view-model with persisted entries")]
	public void GivenAHistoryViewModelWithPersistedEntries() => driver.StoreHasEntries();

	[When(@"the history loads on a background thread")]
	public Task WhenTheHistoryLoadsOnABackgroundThread() => driver.LoadOnBackgroundThread();

	[Then(@"the entries are listed with no cross-thread failure")]
	public void ThenTheEntriesAreListedWithNoCrossThreadFailure() => driver.AssertEntriesListedSafely();

	[Then(@"the architecture guide records the collection-synchronization convention")]
	public void ThenTheArchitectureGuideRecordsTheConvention() => driver.AssertConventionDocumented();
}
