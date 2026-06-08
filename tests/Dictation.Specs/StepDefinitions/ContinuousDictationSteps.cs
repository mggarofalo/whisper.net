// Thin step definitions for the @WHISPER-28 continuous-dictation feature. Each step delegates to the
// ContinuousDictationDriver (injected by the Reqnroll DI plugin); no logic lives here.

using Dictation.Specs.Drivers;
using Reqnroll;

namespace Dictation.Specs.StepDefinitions;

[Binding]
public sealed class ContinuousDictationSteps(ContinuousDictationDriver driver)
{
	[Given(@"continuous dictation mode is active")]
	public void GivenContinuousDictationModeIsActive() => driver.EnterActiveContinuousMode();

	[When(@"an utterance is transcribed and delivered")]
	public Task WhenAnUtteranceIsTranscribedAndDelivered() => driver.TranscribeAndDeliverOneUtterance();

	[When(@"the user presses Esc to exit")]
	public void WhenTheUserPressesEscToExit() => driver.PressEscToExit();

	[Then(@"recording restarts automatically for the next utterance")]
	public void ThenRecordingRestartsAutomatically() => driver.AssertRecordingRestarted();

	[Then(@"recording does not restart")]
	public void ThenRecordingDoesNotRestart() => driver.AssertRecordingDidNotRestart();

	[Then(@"continuous dictation returns to idle")]
	public void ThenContinuousDictationReturnsToIdle() => driver.AssertReturnedToIdle();
}
