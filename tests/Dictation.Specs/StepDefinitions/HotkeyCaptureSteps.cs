// Thin step definitions for the @WHISPER-79 hotkey-capture feature. Each step delegates to the
// HotkeyCaptureDriver (injected by the Reqnroll DI plugin); the modifier list and key are parsed from the
// Gherkin into the Domain vocabulary so the steps stay declarative.

using Dictation.Specs.Drivers;
using Domain.Input;
using Reqnroll;

namespace Dictation.Specs.StepDefinitions;

[Binding]
public sealed class HotkeyCaptureSteps(HotkeyCaptureDriver driver)
{
	[Given(@"the hotkey capture field has loaded the current binding")]
	public Task GivenTheCaptureFieldLoaded() => driver.LoadEditor();

	[When(@"the user presses modifiers ""(.*)"" with the key ""(.*)""")]
	public void WhenTheUserPresses(string modifiers, string key) =>
		driver.Press(ParseModifiers(modifiers), Enum.Parse<KeyboardKey>(key));

	[When(@"the user assigns the captured hotkey")]
	public Task WhenTheUserAssigns() => driver.AssignCaptured();

	[Then(@"the captured hotkey shows ""(.*)""")]
	public void ThenTheCapturedHotkeyShows(string display) => driver.AssertDisplay(display);

	[Then(@"the captured hotkey is valid")]
	public void ThenTheCapturedHotkeyIsValid() => driver.AssertCapturedIsValid();

	[Then(@"nothing is captured")]
	public void ThenNothingIsCaptured() => driver.AssertNothingCaptured();

	[Then(@"the captured hotkey reports a validation error")]
	public void ThenTheCapturedHotkeyReportsAnError() => driver.AssertCapturedHasError();

	[Then(@"the captured hotkey is not persisted")]
	public void ThenTheCapturedHotkeyIsNotPersisted() => driver.AssertNothingPersisted();

	// "Control,Alt" -> KeyModifiers.Control | KeyModifiers.Alt; "None" -> KeyModifiers.None.
	private static KeyModifiers ParseModifiers(string modifiers) =>
		modifiers.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
			.Aggregate(KeyModifiers.None, (current, token) => current | Enum.Parse<KeyModifiers>(token));
}
