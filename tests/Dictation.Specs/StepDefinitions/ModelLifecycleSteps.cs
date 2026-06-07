// Thin bindings for the model lifecycle feature: each step delegates to ModelLifecycleDriver, which
// drives the real lifecycle policy over a fake runtime.

using Dictation.Specs.Drivers;
using Reqnroll;

namespace Dictation.Specs.StepDefinitions;

[Binding]
public sealed class ModelLifecycleSteps(ModelLifecycleDriver driver)
{
	[Given(@"a model has just been loaded with warmup enabled")]
	public Task GivenLoadedWithWarmup() => driver.GivenModelLoadedWithWarmup();

	[Given(@"the ""(.*)"" model is loaded and ready")]
	public Task GivenLoadedAndReady(string modelId) => driver.GivenModelLoadedAndReady(modelId);

	[When(@"the first transcription is requested")]
	public Task WhenFirstTranscription() => driver.RequestFirstTranscription();

	[When(@"the user switches to the ""(.*)"" model")]
	public Task WhenSwitch(string modelId) => driver.SwitchTo(modelId);

	[Then(@"it runs without incurring lazy-initialization latency")]
	public void ThenNoLazyInit() => driver.AssertRanWithoutLazyInitialization();

	[Then(@"the ""(.*)"" model becomes the active ready model")]
	public void ThenActiveReady(string modelId) => driver.AssertActiveReadyModelIs(modelId);

	[Then(@"the ""(.*)"" model's resources are released")]
	public void ThenReleased(string modelId) => driver.AssertModelReleased(modelId);
}
