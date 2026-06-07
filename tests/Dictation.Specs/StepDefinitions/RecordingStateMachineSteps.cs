// Thin step definitions for the @WHISPER-22 recording-state-machine feature. Each step delegates to
// the RecordingStateMachineDriver (injected by the Reqnroll DI plugin); no logic lives here.

using Dictation.Specs.Drivers;
using Reqnroll;

namespace Dictation.Specs.StepDefinitions;

[Binding]
public sealed class RecordingStateMachineSteps(RecordingStateMachineDriver driver)
{
	[Given(@"the recorder is Idle")]
	public void GivenTheRecorderIsIdle()
	{
		// A fresh machine starts Idle; nothing to set up.
	}

	[Given(@"the recorder is Recording")]
	public void GivenTheRecorderIsRecording() => driver.StartRequest();

	[When(@"a start request is received")]
	public void WhenAStartRequestIsReceived() => driver.StartRequest();

	[When(@"a stop request is received")]
	public void WhenAStopRequestIsReceived() => driver.StopRequest();

	[When(@"transcription completes")]
	public void WhenTranscriptionCompletes() => driver.TranscriptionCompletes();

	[When(@"the user presses Esc")]
	public void WhenTheUserPressesEsc() => driver.PressEsc();

	[Then(@"the recorder is {word}")]
	public void ThenTheRecorderIs(string state) => driver.AssertState(state);

	[Then(@"the capture is discarded")]
	public void ThenTheCaptureIsDiscarded() => driver.AssertCaptureDiscarded();

	[Then(@"no text is delivered")]
	public void ThenNoTextIsDelivered() => driver.AssertNoTextDelivered();
}
