// Thin step definitions for the activation-modes feature. Each step delegates to the
// HotkeyActivationDriver (injected by the Reqnroll DI plugin); no logic lives here.

using Dictation.Specs.Drivers;
using Domain.Input;
using Reqnroll;

namespace Dictation.Specs.StepDefinitions;

[Binding]
public sealed class HotkeyActivationSteps(HotkeyActivationDriver driver)
{
	[Given(@"push-to-talk mode with the binding ""(.*)""")]
	public void GivenPushToTalkMode(string binding) => driver.Configure(binding, ActivationMode.PushToTalk);

	[Given(@"toggle mode with the binding ""(.*)""")]
	public void GivenToggleMode(string binding) => driver.Configure(binding, ActivationMode.Toggle);

	[When(@"the chord ""(.*)"" is held")]
	public void WhenTheChordIsHeld(string chord) => driver.HoldChord(chord);

	[When(@"the chord ""(.*)"" is released")]
	public void WhenTheChordIsReleased(string chord) => driver.ReleaseChord(chord);

	[When(@"the chord ""(.*)"" is fully pressed")]
	public void WhenTheChordIsFullyPressed(string chord) => driver.FullPress(chord);

	[When(@"the key ""(.*)"" is pressed and released")]
	public void WhenTheKeyIsPressedAndReleased(string key) => driver.PressUnrelatedKey(key);

	[When(@"the hotkey is reassigned to ""(.*)""")]
	public void WhenTheHotkeyIsReassignedTo(string binding) => driver.Reassign(binding, ActivationMode.PushToTalk);

	[Then(@"recording start is requested (\d+) times?")]
	public void ThenRecordingStartIsRequested(int times) => driver.AssertStartRequested(times);

	[Then(@"recording stop is requested (\d+) times?")]
	public void ThenRecordingStopIsRequested(int times) => driver.AssertStopRequested(times);

	[Then(@"recording cancel is requested (\d+) times?")]
	public void ThenRecordingCancelIsRequested(int times) => driver.AssertCancelRequested(times);
}
