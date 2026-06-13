// Thin step definitions for the settings/feature view-model foundation feature. Each step
// delegates to the SettingsViewModelFoundationDriver (injected by the Reqnroll DI plugin); no logic here.

using Dictation.Specs.Drivers;
using Reqnroll;

namespace Dictation.Specs.StepDefinitions;

[Binding]
public sealed class SettingsViewModelFoundationSteps(SettingsViewModelFoundationDriver driver)
{
	[Given(@"the shell's feature section view-models")]
	public void GivenTheFeatureSectionViewModels()
	{
		// The real feature view-models are composed by AddAppManagement and resolved into the driver.
	}

	[Given(@"the Hotkey section view-model")]
	public void GivenTheHotkeySectionViewModel()
	{
		// Resolved into the driver from the scenario scope.
	}

	[When(@"its current hotkey is set to ""(.*)""")]
	public void WhenItsCurrentHotkeyIsSet(string chord) => driver.SetCurrentHotkey(chord);

	[Then(@"each one is a validation-capable observable view-model")]
	public void ThenEachIsValidationCapable() => driver.AssertEachIsValidationCapableObservable();

	[Then(@"it raises a property-changed notification for the current hotkey")]
	public void ThenItRaisesNotification() => driver.AssertCurrentHotkeyNotified();
}
