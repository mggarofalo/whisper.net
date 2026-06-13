// Thin step definitions for the overlay-feedback feature. Each step delegates to the shared
// LevelOverlayDriver (injected by the Reqnroll DI plugin); no logic lives here. The step text is distinct
// from the level and perceptual-meter overlay steps so Reqnroll binds each unambiguously.

using System;
using Dictation.Specs.Drivers;
using Logic.AppManagement;
using Reqnroll;

namespace Dictation.Specs.StepDefinitions;

[Binding]
public sealed class OverlayFeedbackSteps(LevelOverlayDriver driver)
{
	[Given(@"a dictation recording has started")]
	public void GivenRecordingStarted() => driver.BeginRecording();

	[When(@"(\d+) seconds of recording elapse")]
	public void WhenSecondsElapse(int seconds) => driver.AdvanceSeconds(seconds);

	[When(@"recording stops for transcription")]
	public void WhenRecordingStops() => driver.StopRecording();

	[When(@"the recording nears the duration cap")]
	public void WhenNearsCap() => driver.PublishNearLimit();

	[When(@"the dictation fails")]
	public void WhenDictationFails() => driver.PublishFailure();

	[Then(@"the overlay shows the (.*) state")]
	public void ThenOverlayShowsState(string state) =>
		driver.AssertState(Enum.Parse<OverlayState>(state, ignoreCase: true));

	[Then(@"the overlay elapsed time is at least (\d+) seconds")]
	public void ThenElapsedAtLeast(int seconds) => driver.AssertElapsedAtLeast(seconds);

	[Then(@"the overlay shows the near-cap warning")]
	public void ThenNearCapWarning() => driver.AssertNearCap();

	[Then(@"the overlay is still visible")]
	public void ThenStillVisible() => driver.AssertVisible();
}
