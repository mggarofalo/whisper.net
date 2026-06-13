// Thin step definitions for the end-to-end orchestration feature. Each step delegates to
// the DictationOrchestratorDriver (injected by the Reqnroll DI plugin); no logic lives here.

using Dictation.Specs.Drivers;
using Reqnroll;

namespace Dictation.Specs.StepDefinitions;

[Binding]
public sealed class EndToEndDictationSteps(DictationOrchestratorDriver driver)
{
	[Given(@"the dictation pipeline is idle")]
	public void GivenTheDictationPipelineIsIdle()
	{
		// A fresh orchestrator starts Idle; nothing to set up.
	}

	[Given(@"the dictation pipeline is recording")]
	public void GivenTheDictationPipelineIsRecording() => driver.StartRecording();

	[Given(@"the model will transcribe the captured audio to ""(.*)""")]
	public void GivenTheModelWillTranscribeTheCapturedAudioTo(string text) => driver.ModelWillTranscribeTo(text);

	[When(@"the user starts dictation, speaks, and stops")]
	public Task WhenTheUserStartsDictationSpeaksAndStops() => driver.RunFullDictation();

	[When(@"transcription fails")]
	public Task WhenTranscriptionFails() => driver.TranscriptionFails();

	[Then(@"the captured audio is transcribed")]
	public void ThenTheCapturedAudioIsTranscribed() => driver.AssertTranscribed();

	[Then(@"the text delivered to the active application is ""(.*)""")]
	public void ThenTheTextDeliveredToTheActiveApplicationIs(string text) => driver.AssertDelivered(text);

	[Then(@"no text is delivered to the active application")]
	public void ThenNoTextIsDeliveredToTheActiveApplication() => driver.AssertNothingDelivered();

	[Then(@"the dictation failure is logged")]
	public void ThenTheDictationFailureIsLogged() => driver.AssertFailureLogged();

	[Then(@"the dictation pipeline returns to idle")]
	public void ThenTheDictationPipelineReturnsToIdle() => driver.AssertIdle();
}
