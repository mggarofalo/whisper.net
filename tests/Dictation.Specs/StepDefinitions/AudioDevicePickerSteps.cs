// Thin step definitions for the @WHISPER-80 audio-device picker feature. Each step delegates to the
// AudioDevicePickerDriver (injected by the Reqnroll DI plugin); no logic lives here.

using Dictation.Specs.Drivers;
using Reqnroll;

namespace Dictation.Specs.StepDefinitions;

[Binding]
public sealed class AudioDevicePickerSteps(AudioDevicePickerDriver driver)
{
	[Given(@"two capture devices are available")]
	public void GivenTwoDevicesAvailable() => driver.TwoDevicesAvailable();

	[Given(@"the saved capture device ""(.*)"" is no longer present")]
	public void GivenTheSavedDeviceIsMissing(string deviceId) => driver.SavedDeviceIsMissing(deviceId);

	[When(@"the device list is loaded")]
	public Task WhenTheDeviceListIsLoaded() => driver.LoadDevices();

	[When(@"the user picks the device ""(.*)""")]
	public Task WhenTheUserPicksTheDevice(string deviceId) => driver.PickDevice(deviceId);

	[Then(@"the picker lists ""(.*)"" and ""(.*)""")]
	public void ThenThePickerLists(string first, string second) => driver.AssertListedByName(first, second);

	[Then(@"the device ""(.*)"" is committed")]
	public void ThenTheDeviceIsCommitted(string deviceId) => driver.AssertCommitted(deviceId);

	[Then(@"the picker falls back to the system default")]
	public void ThenThePickerFallsBack() => driver.AssertFellBackToSystemDefault();

	[Then(@"a clear unavailable-device warning is shown")]
	public void ThenAWarningIsShown() => driver.AssertUnavailableWarningShown();
}
