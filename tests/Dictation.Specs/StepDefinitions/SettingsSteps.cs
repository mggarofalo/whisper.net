// Thin step definitions for the settings feature. Each step delegates to the SettingsDriver (injected
// by the Reqnroll DI plugin); no logic lives here.

using Dictation.Specs.Drivers;
using Reqnroll;

namespace Dictation.Specs.StepDefinitions;

[Binding]
public sealed class SettingsSteps(SettingsDriver driver)
{
	[Given("the settings store holds the user's saved settings")]
	public void GivenTheStoreHoldsSavedSettings() => driver.StoreHoldsSavedSettings();

	[When("the current settings are requested")]
	public Task WhenTheCurrentSettingsAreRequested() => driver.RequestCurrentSettings();

	[Then("the saved settings are returned to the caller")]
	public void ThenTheSavedSettingsAreReturned() => driver.AssertSavedSettingsReturned();

	[Given("a valid settings update")]
	public void GivenAValidSettingsUpdate() => driver.PrepareValidUpdate();

	[Given("a settings update with an unknown model id")]
	public void GivenAnUpdateWithAnUnknownModelId() => driver.PrepareUpdateWithUnknownModel();

	[When("the settings update is submitted")]
	public Task WhenTheSettingsUpdateIsSubmitted() => driver.SubmitUpdate();

	[Then("the new settings are written to the settings store")]
	public void ThenTheNewSettingsAreWritten() => driver.AssertSettingsWereSaved();

	[Then("the update is rejected and nothing is written to the settings store")]
	public void ThenTheUpdateIsRejected() => driver.AssertRejectedAndNothingSaved();
}
