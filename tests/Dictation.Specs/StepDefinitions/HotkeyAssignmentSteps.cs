// Thin step definitions for the hotkey-assignment feature. Each step delegates to the
// HotkeyAssignmentDriver (injected by the Reqnroll DI plugin); no logic lives here.

using Dictation.Specs.Drivers;
using Reqnroll;

namespace Dictation.Specs.StepDefinitions;

[Binding]
public sealed class HotkeyAssignmentSteps(HotkeyAssignmentDriver driver)
{
	[Given(@"settings persisted with the hotkey ""(.*)""")]
	public void GivenSettingsPersistedWithTheHotkey(string chord) => driver.PersistSettingsWithHotkey(chord);

	[Given(@"the hotkey pipeline has started")]
	public async Task GivenTheHotkeyPipelineHasStarted() => await driver.StartPipeline();

	[Given(@"the hotkey section is open")]
	[When(@"the user opens the hotkey section")]
	public async Task WhenTheUserOpensTheHotkeySection() => await driver.OpenHotkeySection();

	[When(@"the user captures and assigns the hotkey ""(.*)""")]
	public async Task WhenTheUserCapturesAndAssignsTheHotkey(string chord) => await driver.CaptureAndAssign(chord);

	[When(@"the application is relaunched")]
	public async Task WhenTheApplicationIsRelaunched() => await driver.RelaunchApplication();

	[Then(@"the hotkey section shows the current binding ""(.*)""")]
	public void ThenTheHotkeySectionShowsTheCurrentBinding(string chord) => driver.AssertCurrentBindingShown(chord);

	[Then(@"the live matcher is bound to ""(.*)""")]
	public void ThenTheLiveMatcherIsBoundTo(string chord) => driver.AssertMatcherBoundTo(chord);

	[Then(@"the live matcher is no longer bound to ""(.*)""")]
	public void ThenTheLiveMatcherIsNoLongerBoundTo(string chord) => driver.AssertMatcherNotBoundTo(chord);

	[Then(@"the persisted settings hold the hotkey ""(.*)""")]
	public void ThenThePersistedSettingsHoldTheHotkey(string chord) => driver.AssertPersistedHotkeyIs(chord);
}
