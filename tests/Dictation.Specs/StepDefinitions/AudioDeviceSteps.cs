// Thin step definitions for the @WHISPER-13 device-selection feature. Each step delegates to the
// AudioDeviceDriver (injected by the Reqnroll DI plugin); no logic lives here.

using Dictation.Specs.Drivers;
using Reqnroll;

namespace Dictation.Specs.StepDefinitions;

[Binding]
public sealed class AudioDeviceSteps(AudioDeviceDriver driver)
{
	[Given(@"the capture devices ""(.*)"" and ""(.*)"" are available with ""(.*)"" as the default")]
	public void GivenDevicesAvailable(string first, string second, string defaultName) =>
		driver.DevicesAvailable(first, second, defaultName);

	[Given(@"the user selects capture device ""(.*)""")]
	public void GivenUserSelects(string id) => driver.SelectDevice(id);

	[Given(@"the user follows the system default capture device")]
	public void GivenUserFollowsDefault() => driver.FollowSystemDefault();

	[Given(@"the user has selected a capture device that is no longer present")]
	public void GivenUserSelectedMissing() => driver.SelectMissingDevice();

	[When(@"the application restarts")]
	public void WhenApplicationRestarts() => driver.Restart();

	[When(@"the system default capture device changes to ""(.*)""")]
	public void WhenDefaultChangesTo(string id) => driver.DefaultChangesTo(id);

	[When(@"capture resolves the device to use")]
	public void WhenCaptureResolves() => driver.Resolve();

	[Then(@"capture uses device ""(.*)""")]
	public void ThenCaptureUses(string id) => driver.AssertUsesDevice(id);

	[Then(@"the device substitution is reported")]
	public void ThenSubstitutionReported() => driver.AssertSubstitutionReported();
}
