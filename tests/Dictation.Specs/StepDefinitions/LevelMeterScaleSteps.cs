// Thin step definitions for the perceptual-meter feature. Each step delegates to the shared
// LevelOverlayDriver (injected by the Reqnroll DI plugin); no logic lives here. The step text is distinct
// from the level-overlay steps so Reqnroll binds each unambiguously.

using Dictation.Specs.Drivers;
using Reqnroll;

namespace Dictation.Specs.StepDefinitions;

[Binding]
public sealed class LevelMeterScaleSteps(LevelOverlayDriver driver)
{
	[Given(@"recording is underway")]
	public void GivenRecordingIsUnderway() => driver.BeginRecording();

	[When(@"the microphone receives sustained normal-volume speech")]
	public void WhenNormalSpeech() => driver.ReceiveSustainedAudio(0.05f);

	[When(@"the microphone receives sustained near-silence")]
	public void WhenNearSilence() => driver.ReceiveSustainedAudio(0.0005f);

	[When(@"the microphone receives sustained loud speech")]
	public void WhenLoudSpeech() => driver.ReceiveSustainedAudio(0.5f);

	[Then(@"the overlay meter sits in the mid-range")]
	public void ThenMidRange() => driver.AssertMeterMidRange();

	[Then(@"the overlay meter sits at or near zero")]
	public void ThenNearZero() => driver.AssertMeterNearZero();

	[Then(@"the overlay meter approaches full scale without pegging")]
	public void ThenApproachesFull() => driver.AssertMeterApproachesFullWithoutPegging();
}
