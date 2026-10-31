// Thin step definitions for the silence-hint feature. Each step delegates to the shared
// LevelOverlayDriver (injected by the Reqnroll DI plugin); no logic lives here. Recording start/stop,
// elapsed time and visibility reuse the overlay-feedback steps.

using Dictation.Specs.Drivers;
using Reqnroll;

namespace Dictation.Specs.StepDefinitions;

[Binding]
public sealed class SilenceHintSteps(LevelOverlayDriver driver)
{
	[Given(@"(\d+) seconds of silent recording elapse")]
	[When(@"(\d+) seconds of silent recording elapse")]
	public void SilentSecondsElapse(int seconds) => driver.CaptureFor(seconds, amplitude: 0f);

	[When(@"quiet background noise is captured for (\d+) seconds")]
	public void QuietNoiseFor(int seconds) => driver.CaptureFor(seconds, amplitude: 0.001f);

	[Given(@"the user starts speaking")]
	[When(@"the user starts speaking")]
	public void UserStartsSpeaking() => driver.ReceiveSustainedAudio(0.05f);

	[When(@"the recording ends with no audio captured")]
	public void RecordingEndsWithNoAudio() => driver.EndWithNoAudio();

	[Then(@"the overlay says ""(.*)""")]
	public void OverlaySays(string message) => driver.AssertMessage(message);

	[Then(@"the overlay shows no message")]
	public void OverlayShowsNoMessage() => driver.AssertMessage(null);

	[Then(@"the overlay is hidden")]
	public void OverlayIsHidden() => driver.AssertOverlayHidden();
}
