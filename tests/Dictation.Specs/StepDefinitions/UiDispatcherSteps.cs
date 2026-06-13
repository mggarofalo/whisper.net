// Thin step definitions for the UI-dispatcher-seam feature. Each step delegates to the
// UiDispatcherDriver (injected by the Reqnroll DI plugin); no logic lives here.

using Dictation.Specs.Drivers;
using Reqnroll;

namespace Dictation.Specs.StepDefinitions;

[Binding]
public sealed class UiDispatcherSteps(UiDispatcherDriver driver)
{
	[Given(@"a tray icon view-model bound to a test UI dispatcher")]
	public void GivenATrayIconViewModelBoundToATestUIDispatcher() => driver.CreateTrayViewModel();

	[Given(@"a level overlay view-model bound to a test UI dispatcher")]
	public void GivenALevelOverlayViewModelBoundToATestUIDispatcher() => driver.CreateOverlayViewModel();

	[Given(@"the caller is already on the UI thread")]
	public void GivenTheCallerIsAlreadyOnTheUIThread() => driver.GrantUiThreadAccess();

	[When(@"the recording state changes off the UI thread")]
	[When(@"the recording state changes")]
	public void WhenTheRecordingStateChanges() => driver.StartRecording();

	[When(@"recording starts and an audio frame arrives off the UI thread")]
	public void WhenRecordingStartsAndAnAudioFrameArrives() => driver.StartRecordingAndEmitFrame();

	[Then(@"the status update is marshaled through the dispatcher seam")]
	public void ThenTheStatusUpdateIsMarshaledThroughTheDispatcherSeam() => driver.AssertStatusUpdateWasMarshaled();

	[Then(@"the update is applied without a dispatcher round-trip")]
	public void ThenTheUpdateIsAppliedWithoutADispatcherRoundTrip() => driver.AssertNoDispatcherRoundTrip();

	[Then(@"the tray view-model reflects the recording status and tooltip")]
	public void ThenTheTrayViewModelReflectsTheRecordingStatusAndTooltip() => driver.AssertTrayReflectsRecording();

	[Then(@"the level update is posted without blocking the calling thread")]
	public void ThenTheLevelUpdateIsPostedWithoutBlocking() => driver.AssertLevelUpdateWasPostedWithoutBlocking();

	[Then(@"the overlay view-model reflects the new level")]
	public void ThenTheOverlayViewModelReflectsTheNewLevel() => driver.AssertOverlayReflectsLevel();

	[Then(@"no production source file references the WPF application dispatcher")]
	public void ThenNoProductionSourceFileReferencesTheWPFApplicationDispatcher() =>
		driver.AssertNoProductionSourceReferencesWpfDispatcher();
}
