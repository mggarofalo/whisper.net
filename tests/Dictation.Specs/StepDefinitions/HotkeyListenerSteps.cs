// Thin step definitions for the global-hotkey-listening feature. Each step delegates to
// the HotkeyListenerDriver (injected by the Reqnroll DI plugin); no logic lives here.

using Dictation.Specs.Drivers;
using Reqnroll;

namespace Dictation.Specs.StepDefinitions;

[Binding]
public sealed class HotkeyListenerSteps(HotkeyListenerDriver driver)
{
	[Given(@"the global hotkey listener is started")]
	public void GivenTheListenerIsStarted() => driver.Start();

	[When(@"the chord ""(.*)"" is pressed at the OS hook")]
	public void WhenTheChordIsPressed(string chord) => driver.PressChord(chord);

	[When(@"the key ""(.*)"" is pressed at the OS hook")]
	public void WhenTheKeyIsPressed(string key) => driver.PressKey(key);

	[When(@"the key ""(.*)"" is released at the OS hook")]
	public void WhenTheKeyIsReleased(string key) => driver.ReleaseKey(key);

	[When(@"the listener is disposed")]
	public void WhenTheListenerIsDisposed() => driver.Dispose();

	[Then(@"a key-down is observed for ""(.*)"" with modifiers ""(.*)""")]
	public void ThenAKeyDownIsObserved(string key, string modifiers) => driver.AssertKeyDown(key, modifiers);

	[Then(@"a key-up is observed for ""(.*)"" with modifiers ""(.*)""")]
	public void ThenAKeyUpIsObserved(string key, string modifiers) => driver.AssertKeyUp(key, modifiers);

	[Then(@"the hook event loop has stopped")]
	public void ThenTheHookEventLoopHasStopped() => driver.AssertHookStopped();

	[Then(@"no further key events are observed")]
	public void ThenNoFurtherKeyEvents() => driver.ProduceStrayKey();
}
