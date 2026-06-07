// Thin bindings for the on-device transcription feature: each step delegates to
// WhisperTranscriptionDriver, which builds and runs the real Whisper.net adapter over a fake engine.

using Dictation.Specs.Drivers;
using Reqnroll;

namespace Dictation.Specs.StepDefinitions;

[Binding]
public sealed class OnDeviceTranscriptionSteps(WhisperTranscriptionDriver driver)
{
	[Given(@"a loaded model and a 16 kHz mono PCM clip of ""(.*)""")]
	public void GivenLoadedModel(string text) => driver.GivenLoadedModelTranscribingTo(text);

	[Given(@"a model path that does not exist on disk")]
	public void GivenMissingModel() => driver.GivenModelPathThatDoesNotExist();

	[When(@"the transcriber processes the clip")]
	public Task WhenTranscribe() => driver.Transcribe();

	[Then(@"the recognized text is ""(.*)""")]
	public void ThenRecognizedText(string expected) => driver.AssertRecognizedText(expected);

	[Then(@"no network egress occurs during transcription")]
	public void ThenNoNetwork() => driver.AssertNoNetworkEgress();

	[Then(@"a typed model-not-found error is returned")]
	public void ThenModelNotFound() => driver.AssertModelNotFoundError();

	[Then(@"the application does not crash")]
	public void ThenNoCrash() => driver.AssertDidNotCrash();
}
