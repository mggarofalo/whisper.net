// Thin step definitions for the @WHISPER-21 audio-feedback feature. Each step delegates to the
// AudioFeedbackDriver (injected by the Reqnroll DI plugin); no logic lives here.

using Dictation.Specs.Drivers;
using Reqnroll;

namespace Dictation.Specs.StepDefinitions;

[Binding]
public sealed class AudioFeedbackSteps(AudioFeedbackDriver driver)
{
	[Given(@"audio feedback is enabled")]
	public void GivenAudioFeedbackIsEnabled() => driver.EnableFeedback();

	[Given(@"audio feedback is disabled")]
	public void GivenAudioFeedbackIsDisabled() => driver.DisableFeedback();

	[When(@"the pipeline reaches ""(.*)""")]
	public Task WhenThePipelineReaches(string @event) => driver.ReachEvent(@event);

	[Then(@"the ""(.*)"" sound is played")]
	public void ThenTheSoundIsPlayed(string @event) => driver.AssertSoundPlayed(@event);

	[Then(@"no sound is played")]
	public void ThenNoSoundIsPlayed() => driver.AssertNoSoundPlayed();
}
