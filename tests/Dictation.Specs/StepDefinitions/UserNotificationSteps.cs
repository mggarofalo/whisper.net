// Thin step definitions for the @WHISPER-95 error-surfacing feature. Each step delegates to the
// UserNotificationDriver (injected by the Reqnroll DI plugin); no logic lives here.

using Dictation.Specs.Drivers;
using Reqnroll;

namespace Dictation.Specs.StepDefinitions;

[Binding]
public sealed class UserNotificationSteps(UserNotificationDriver driver)
{
	[Given(@"a dictation pipeline whose transcription fails")]
	public void GivenADictationPipelineWhoseTranscriptionFails() => driver.TranscriptionWillFail();

	[When(@"an utterance is recorded and stopped")]
	public Task WhenAnUtteranceIsRecordedAndStopped() => driver.RecordAndStop();

	[Given(@"a dictation pipeline whose capture device fails mid-recording")]
	public void GivenADictationPipelineWhoseCaptureDeviceFails()
	{
		// The failure is raised in the When; nothing to pre-configure on the fake device.
	}

	[When(@"recording starts and the device failure strikes")]
	public void WhenRecordingStartsAndTheDeviceFailureStrikes() => driver.StartAndFailDevice();

	[Then(@"a user notification reports the dictation failure")]
	public void ThenAUserNotificationReportsTheDictationFailure() => driver.AssertFailureNotified();

	[Then(@"the pipeline has returned to idle")]
	public void ThenThePipelineHasReturnedToIdle() => driver.AssertPipelineIdle();

	[Given(@"a tray notifier bound to a test UI dispatcher and a recording balloon")]
	public void GivenATrayNotifierWithRecordingBalloon() => driver.CreateNotifierWithRecordingBalloon();

	[Given(@"a tray notifier with no balloon presenter attached")]
	public void GivenATrayNotifierWithNoBalloonPresenter() => driver.CreateNotifierWithoutPresenter();

	[Given(@"a tray notifier whose balloon presenter throws")]
	public void GivenATrayNotifierWhoseBalloonPresenterThrows() => driver.CreateNotifierWithThrowingPresenter();

	[When(@"an error notification is raised off the UI thread")]
	public void WhenAnErrorNotificationIsRaisedOffTheUIThread() => driver.RaiseErrorOffUiThread();

	[Then(@"the balloon request was marshaled through the dispatcher seam")]
	public void ThenTheBalloonRequestWasMarshaledThroughTheDispatcherSeam() => driver.AssertBalloonMarshaledThroughSeam();

	[Then(@"the notification is swallowed without an exception")]
	public void ThenTheNotificationIsSwallowedWithoutAnException() => driver.AssertSwallowedWithWarning();

	[Then(@"the dispatcher exception handler notifies the user with a non-technical message")]
	public void ThenTheDispatcherExceptionHandlerNotifies() => driver.AssertDispatcherExceptionHandlerNotifies();
}
