// Thin step definitions for the @WHISPER-129 model warm-up status feature. Each step delegates to the
// ModelWarmupStatusDriver (injected by the Reqnroll DI plugin); no logic lives here.

using Dictation.Specs.Drivers;
using Reqnroll;

namespace Dictation.Specs.StepDefinitions;

[Binding]
public sealed class ModelWarmupStatusSteps(ModelWarmupStatusDriver driver)
{
	[Given(@"the Home dashboard is open and the model is not warming up")]
	public async Task GivenTheDashboardIsOpenAndNotWarming() => await driver.OpenDashboard();

	[When(@"the model begins warming up")]
	public void WhenTheModelBeginsWarmingUp() => driver.BeginWarmup();

	[When(@"the model finishes warming up")]
	public void WhenTheModelFinishesWarmingUp() => driver.CompleteWarmup();

	[Then(@"the dictation overlay shows the warming state")]
	public void ThenTheOverlayShowsWarming() => driver.AssertOverlayShowsWarming();

	[Then(@"the Home dashboard shows the warming status")]
	public void ThenTheDashboardShowsWarming() => driver.AssertDashboardShowsWarming();

	[Then(@"the dictation overlay is hidden")]
	public void ThenTheOverlayIsHidden() => driver.AssertOverlayHidden();

	[Then(@"the Home dashboard no longer shows the warming status")]
	public void ThenTheDashboardNoLongerShowsWarming() => driver.AssertDashboardNotWarming();
}
