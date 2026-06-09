// Thin step definitions for the @WHISPER-76 hotkey-reassignment feature. Each step delegates to the
// HotkeyConfigurationDriver (injected by the Reqnroll DI plugin); no logic lives here.

using Dictation.Specs.Drivers;
using Reqnroll;

namespace Dictation.Specs.StepDefinitions;

[Binding]
public sealed class HotkeyReassignmentSteps(HotkeyConfigurationDriver driver)
{
	[Given(@"a persisted hotkey ""(.*)""")]
	public async Task GivenAPersistedHotkey(string chord) => await driver.PersistHotkey(chord);

	[Given(@"the dictation pipeline has started")]
	[When(@"the dictation pipeline starts")]
	public async Task WhenThePipelineStarts() => await driver.StartPipeline();

	[When(@"the dictation hotkey is changed to ""(.*)""")]
	public async Task WhenTheHotkeyIsChanged(string chord) => await driver.ChangeHotkey(chord);

	[Then(@"the activation controller matches the chord ""(.*)""")]
	public void ThenControllerMatchesChord(string chord) => driver.AssertControllerMatchesChord(chord);
}
