// Thin step definitions for the native settings-validation feature. Each step delegates to
// the SettingsValidationDriver (injected by the Reqnroll DI plugin); no logic lives here.

using Dictation.Specs.Drivers;
using Reqnroll;

namespace Dictation.Specs.StepDefinitions;

[Binding]
public sealed class SettingsValidationSteps(SettingsValidationDriver driver)
{
	[Given(@"the hotkey settings editor has loaded the current binding")]
	public Task GivenTheEditorHasLoaded() => driver.LoadEditor();

	[When(@"the user enters the hotkey ""(.*)""")]
	public void WhenTheUserEntersTheHotkey(string chord) => driver.EnterHotkey(chord);

	[When(@"the user saves the hotkey")]
	public Task WhenTheUserSavesTheHotkey() => driver.SaveHotkey();

	[Then(@"the hotkey field reports a validation error")]
	public void ThenTheFieldReportsAnError() => driver.AssertFieldHasError();

	[Then(@"the hotkey field reports no validation error")]
	public void ThenTheFieldReportsNoError() => driver.AssertFieldHasNoError();

	[Then(@"no settings update is persisted")]
	public void ThenNothingPersisted() => driver.AssertNothingPersisted();

	[Then(@"the binding ""(.*)"" is persisted")]
	public void ThenTheBindingIsPersisted(string chord) => driver.AssertBindingPersisted(chord);
}
