// Thin step definitions for the push-to-talk delivery feature. Each step delegates to the
// TranscriptionDriver (injected by the Reqnroll DI plugin); no logic lives here.

using Dictation.Specs.Drivers;
using Reqnroll;

namespace Dictation.Specs.StepDefinitions;

[Binding]
public sealed class PushToTalkSteps(TranscriptionDriver driver)
{
	[Given(@"the model will transcribe the audio to ""(.*)""")]
	public void GivenTheModelWillTranscribeTheAudioTo(string text) => driver.ModelWillTranscribeTo(text);

	[Given(@"the captured audio is silent")]
	public void GivenTheCapturedAudioIsSilent() => driver.CapturedAudioIsSilent();

	[When(@"push-to-talk is released")]
	public Task WhenPushToTalkIsReleased() => driver.ReleasePushToTalk();

	[Then(@"the model is not asked to transcribe")]
	public void ThenTheModelIsNotAskedToTranscribe() => driver.AssertNotTranscribed();

	[Then(@"the text delivered to the focused field is ""(.*)""")]
	public void ThenTheTextDeliveredToTheFocusedFieldIs(string text) => driver.AssertDelivered(text);

	[Then(@"no text is delivered to the focused field")]
	public void ThenNoTextIsDeliveredToTheFocusedField() => driver.AssertNothingDelivered();
}
