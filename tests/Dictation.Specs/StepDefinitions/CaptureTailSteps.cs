// Thin step definitions for the capture-tail feature. Each step delegates to the
// CaptureTailDriver (injected by the Reqnroll DI plugin); no logic lives here.

using Dictation.Specs.Drivers;
using Reqnroll;

namespace Dictation.Specs.StepDefinitions;

[Binding]
public sealed class CaptureTailSteps(CaptureTailDriver driver)
{
	[Given(@"the user is dictating a short phrase")]
	public void GivenTheUserIsDictatingAShortPhrase() => driver.StartDictatingShortPhrase();

	[When(@"the user releases the chord")]
	public void WhenTheUserReleasesTheChord() => driver.ReleaseChord();

	[When(@"the device delivers the remaining audio during the grace window")]
	public void WhenTheDeviceDeliversTheRemainingAudioDuringTheGraceWindow() => driver.DeviceDeliversRemainingAudio();

	[When(@"the post-release grace window elapses")]
	public Task WhenThePostReleaseGraceWindowElapses() => driver.GraceWindowElapses();

	[Then(@"the clip handed to the transcriber contains the post-release audio")]
	public void ThenTheClipHandedToTheTranscriberContainsThePostReleaseAudio() =>
		driver.AssertTranscribedClipContainsPostReleaseAudio();

	[Given(@"a clip that ends in (\d+) ms of quiet trailing speech")]
	public void GivenAClipThatEndsInQuietTrailingSpeech(int milliseconds) =>
		driver.ClipEndsInQuietTrailingSpeech(milliseconds);

	[Given(@"a clip that ends in (\d+) ms of dead air")]
	public void GivenAClipThatEndsInDeadAir(int milliseconds) => driver.ClipEndsInDeadAir(milliseconds);

	[When(@"trailing silence is trimmed")]
	public void WhenTrailingSilenceIsTrimmed() => driver.TrimTrailingSilence();

	[Then(@"the quiet trailing speech is preserved")]
	public void ThenTheQuietTrailingSpeechIsPreserved() => driver.AssertQuietTrailingSpeechPreserved();

	[Then(@"the dead air is trimmed away leaving only a short pad")]
	public void ThenTheDeadAirIsTrimmedAwayLeavingOnlyAShortPad() => driver.AssertDeadAirTrimmedToPad();
}
