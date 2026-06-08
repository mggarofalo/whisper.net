// Thin step definitions for the @WHISPER-26 level-overlay feature. Each step delegates to the
// LevelOverlayDriver (injected by the Reqnroll DI plugin); no logic lives here.

using Dictation.Specs.Drivers;
using Reqnroll;

namespace Dictation.Specs.StepDefinitions;

[Binding]
public sealed class LevelOverlaySteps(LevelOverlayDriver driver)
{
	[Given(@"dictation is idle and the overlay is hidden")]
	public void GivenDictationIsIdleAndTheOverlayIsHidden()
	{
		// A fresh controller starts hidden (Idle); nothing to set up.
	}

	[Given(@"the level overlay is visible while recording")]
	public void GivenTheLevelOverlayIsVisibleWhileRecording() => driver.StartRecording();

	[When(@"recording starts")]
	public void WhenRecordingStarts() => driver.StartRecording();

	[When(@"recording stops")]
	public void WhenRecordingStops() => driver.StopRecording();

	[Then(@"the level overlay becomes visible")]
	public void ThenTheLevelOverlayBecomesVisible() => driver.AssertOverlayVisible();

	[Then(@"it reflects the current microphone input level")]
	public void ThenItReflectsTheCurrentMicrophoneInputLevel() => driver.AssertReflectsInputLevel();

	[Then(@"the level overlay is hidden")]
	public void ThenTheLevelOverlayIsHidden() => driver.AssertOverlayHidden();
}
