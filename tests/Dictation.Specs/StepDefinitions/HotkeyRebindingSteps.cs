// Thin step definitions for the @WHISPER-30 hotkey-rebinding feature. Each step delegates to the
// HotkeyRebindingDriver (injected by the Reqnroll DI plugin); no logic lives here.

using Dictation.Specs.Drivers;
using Reqnroll;

namespace Dictation.Specs.StepDefinitions;

[Binding]
public sealed class HotkeyRebindingSteps(HotkeyRebindingDriver driver)
{
	[Given(@"hotkey capture has started")]
	public void GivenHotkeyCaptureHasStarted() => driver.BeginCapture();

	[When(@"the chord ""(.*)"" is captured")]
	public void WhenTheChordIsCaptured(string chord) => driver.CaptureChord(chord);

	[When(@"only ""(.*)"" is pressed and released")]
	public void WhenOnlyIsPressedAndReleased(string chord) => driver.CaptureChord(chord);

	[When(@"""(.*)"" is pressed during capture")]
	public void WhenKeyIsPressedDuringCapture(string key) => driver.CaptureSingleKey(key);

	[Then(@"capture resolves to the binding ""(.*)""")]
	public void ThenCaptureResolvesToTheBinding(string chord) => driver.AssertCaptured(chord);

	[Then(@"the capture is rejected")]
	public void ThenTheCaptureIsRejected() => driver.AssertRejected();

	[Then(@"the capture is cancelled")]
	public void ThenTheCaptureIsCancelled() => driver.AssertCancelled();

	[Then(@"holding ""(.*)"" (?:still )?triggers recording")]
	public void ThenHoldingTriggersRecording(string chord)
	{
		driver.HoldChordOnController(chord);
		driver.AssertRecordingTriggered();
	}
}
