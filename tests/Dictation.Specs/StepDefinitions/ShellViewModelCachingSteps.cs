// Thin step definitions for the @WHISPER-89 shell view-model caching feature. Each step delegates to the
// ShellNavigationDriver (injected by the Reqnroll DI plugin); no logic lives here.

using Dictation.Specs.Drivers;
using Reqnroll;

namespace Dictation.Specs.StepDefinitions;

[Binding]
public sealed class ShellViewModelCachingSteps(ShellNavigationDriver driver)
{
	[Given(@"the Model section has loaded its list and resolved the active model")]
	public async Task GivenTheModelSectionHasLoaded() => await driver.LoadModelSectionAndRemember();

	[When(@"the user navigates away to the ""(.*)"" section and back to the ""(.*)"" section")]
	public void WhenTheUserNavigatesAwayAndBack(string away, string back) => driver.NavigateAwayAndBack(away, back);

	[Then(@"the Model section is the same view-model instance as before")]
	public void ThenTheModelSectionIsTheSameInstance() => driver.AssertActiveIsRememberedInstance();

	[Then(@"the loaded model list and active selection are still present")]
	public void ThenTheSelectionSurvived() => driver.AssertModelSelectionSurvived();
}
