// Thin step definitions for the view-resolution feature. The artifact checks delegate to
// the ViewResolutionDriver; the device-picker commit behavior (the logic that moved out of the view's
// code-behind) delegates to the AudioDevicePickerDriver. No logic lives here.

using Dictation.Specs.Drivers;
using Reqnroll;

namespace Dictation.Specs.StepDefinitions;

[Binding]
public sealed class ViewResolutionSteps(ViewResolutionDriver driver, AudioDevicePickerDriver picker)
{
	[Then(@"the architecture guide records the implicit-DataTemplate view-resolution convention")]
	public void ThenTheArchitectureGuideRecordsTheConvention() => driver.AssertConventionDocumented();

	[Then(@"each registered navigation section has a data template mapping its view-model to a view")]
	public void ThenEachRegisteredNavigationSectionHasADataTemplate() => driver.AssertEverySectionHasTemplate();

	[Then(@"no feature view code-behind contains logic beyond its constructor")]
	public void ThenNoFeatureViewCodeBehindContainsLogic() => driver.AssertFeatureViewCodeBehindIsConstructionOnly();

	[Then(@"no view switches on property-change names")]
	public void ThenNoViewSwitchesOnPropertyChangeNames() => driver.AssertNoViewSwitchesOnPropertyNames();

	[Given(@"the device picker has loaded two available devices")]
	public async Task GivenTheDevicePickerHasLoadedTwoAvailableDevices()
	{
		picker.TwoDevicesAvailable();
		await picker.LoadDevices();
	}

	[When(@"the selected device changes to ""(.*)""")]
	public Task WhenTheSelectedDeviceChangesTo(string deviceId) => picker.ChangeSelection(deviceId);

	[Then(@"the device choice ""(.*)"" is persisted exactly once")]
	public void ThenTheDeviceChoiceIsPersistedExactlyOnce(string deviceId) => picker.AssertCommittedExactlyOnce(deviceId);

	[Given(@"a persisted capture device that is no longer connected")]
	public void GivenAPersistedCaptureDeviceThatIsNoLongerConnected()
	{
		picker.TwoDevicesAvailable();
		picker.SavedDeviceIsMissing("mic-gone");
	}

	[When(@"the device picker loads and falls back to the system default")]
	public async Task WhenTheDevicePickerLoadsAndFallsBack()
	{
		await picker.LoadDevices();
		picker.AssertFellBackToSystemDefault();
	}

	[Then(@"no device choice is persisted")]
	public void ThenNoDeviceChoiceIsPersisted() => picker.AssertNothingCommitted();
}
