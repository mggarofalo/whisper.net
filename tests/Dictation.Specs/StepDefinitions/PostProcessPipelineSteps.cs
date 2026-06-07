// Thin step definitions for the @WHISPER-41 post-process pipeline feature. Each step delegates to the
// PostProcessPipelineDriver (injected by the Reqnroll DI plugin); no logic lives here.

using Dictation.Specs.Drivers;
using Reqnroll;

namespace Dictation.Specs.StepDefinitions;

[Binding]
public sealed class PostProcessPipelineSteps(PostProcessPipelineDriver driver)
{
	[Given(@"filler removal is currently disabled")]
	public void GivenFillerRemovalIsDisabled() => driver.FillerRemovalIsDisabled();

	[When(@"the user enables filler removal in configuration")]
	public Task WhenTheUserEnablesFillerRemoval() => driver.EnableFillerRemoval();

	[Given(@"the rephrase client rewrites text to ""(.*)""")]
	public void GivenTheRephraseClientRewritesTextTo(string rewritten) => driver.RephraseRewritesTo(rewritten);

	[Given(@"the default transform is configured to ""(.*)""")]
	public Task GivenTheDefaultTransformIsConfiguredTo(string name) => driver.ConfigureDefaultTransform(name);

	[Given(@"a post-process configuration whose default transform is the unknown ""(.*)""")]
	public void GivenAConfigurationWithUnknownTransform(string name) => _pendingTransform = name;

	[When(@"the configuration is applied")]
	public Task WhenTheConfigurationIsApplied() => driver.ApplyConfiguration(_pendingTransform);

	[When(@"the pipeline is left with that unknown default transform")]
	public void WhenThePipelineIsLeftWithUnknownTransform() => driver.LeavePipelineWithDefaultTransform(_pendingTransform);

	[When(@"the transcription ""(.*)"" is post-processed")]
	public Task WhenTheTranscriptionIsPostProcessed(string text) => driver.PostProcess(text);

	[Then(@"the post-processed text is ""(.*)""")]
	public void ThenThePostProcessedTextIs(string expected) => driver.AssertResult(expected);

	[Then(@"a clear validation error about the transform is reported")]
	public void ThenAClearValidationErrorAboutTheTransformIsReported() => driver.AssertValidationErrorAboutTransform();

	private string _pendingTransform = string.Empty;
}
