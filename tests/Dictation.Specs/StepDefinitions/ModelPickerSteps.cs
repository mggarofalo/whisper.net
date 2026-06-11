// Thin step definitions for the @WHISPER-27 model picker feature. Each step delegates to the
// ModelPickerDriver (injected by the Reqnroll DI plugin); no logic lives here.

using Dictation.Specs.Drivers;
using Reqnroll;

namespace Dictation.Specs.StepDefinitions;

[Binding]
public sealed class ModelPickerSteps(ModelPickerDriver driver)
{
	[Given(@"the model catalog is available")]
	public void GivenTheModelCatalogIsAvailable()
	{
		// The real WhisperModelCatalog is composed by AddModelManagement; nothing to set up.
	}

	[Given(@"the model picker lists a downloaded model ""(.*)""")]
	public async Task GivenThePickerListsADownloadedModel(string id) => await driver.GivenDownloadedModelListed(id);

	[Given(@"the user selects a model ""(.*)"" that is not yet downloaded")]
	public async Task GivenAnUndownloadedModelIsListed(string id) => await driver.GivenUndownloadedModelListed(id);

	[Given(@"the user selects a model ""(.*)"" whose download will fail")]
	public async Task GivenAModelWhoseDownloadWillFail(string id) => await driver.GivenModelWhoseDownloadWillFail(id);

	[Given(@"a model ""(.*)"" is persisted as active but not yet loaded")]
	public void GivenPersistedActiveNotLoaded(string id) => driver.GivenPersistedActiveModelNotYetLoaded(id);

	[When(@"the model list is loaded")]
	public async Task WhenTheModelListIsLoaded() => await driver.LoadList();

	[When(@"the user selects model ""(.*)""")]
	public async Task WhenTheUserSelectsModel(string id) => await driver.SelectModel(id);

	[When(@"the download proceeds")]
	[When(@"the download is attempted")]
	public async Task WhenTheDownloadProceeds() => await driver.SelectTargetModel();

	[Then(@"each listed model shows speed, accuracy, and memory ratings")]
	public void ThenEachModelShowsRatings() => driver.AssertRatingsListed();

	[Then(@"a switch-active-model request is dispatched for ""(.*)""")]
	public void ThenASwitchRequestIsDispatched(string id) => driver.AssertSwitchDispatched(id);

	[Then(@"the selected model is persisted as the active model ""(.*)""")]
	public void ThenTheSelectedModelIsPersisted(string id) => driver.AssertActiveModelPersisted(id);

	[Then(@"the view shows ""(.*)"" as active")]
	public void ThenTheViewShowsActive(string id) => driver.AssertViewShowsActive(id);

	[Then(@"download progress is shown")]
	public void ThenDownloadProgressIsShown() => driver.AssertProgressShown();

	[Then(@"on completion the model becomes active")]
	public void ThenOnCompletionTheModelBecomesActive() => driver.AssertTargetBecomesActive();

	[Then(@"the download is marked failed")]
	public void ThenTheDownloadIsMarkedFailed() => driver.AssertDownloadFailed();

	[Then(@"the model is not made active")]
	public void ThenTheModelIsNotMadeActive() => driver.AssertTargetNotActivated();
}
