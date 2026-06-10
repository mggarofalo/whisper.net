// Thin step definitions for the @WHISPER-111 long-dictation feature. Each step delegates to the
// LongDictationDriver (injected by the Reqnroll DI plugin); no logic lives here.

using Dictation.Specs.Drivers;
using Reqnroll;

namespace Dictation.Specs.StepDefinitions;

[Binding]
public sealed class LongDictationSteps(LongDictationDriver driver)
{
	[Given(@"a recording soft limit of (\d+) ms")]
	public void GivenARecordingSoftLimitOfMs(int milliseconds) => driver.ConfigureSoftLimit(milliseconds);

	[Given(@"the user dictates past the soft limit")]
	public void GivenTheUserDictatesPastTheSoftLimit() => driver.DictatePastTheSoftLimit();

	[When(@"the user stops dictating")]
	public Task WhenTheUserStopsDictating() => driver.StopDictating();

	[Then(@"the clip handed to the transcriber contains the audio spoken before the limit")]
	public void ThenTheClipContainsTheAudioSpokenBeforeTheLimit() => driver.AssertClipContainsPreLimitAudio();

	[Then(@"the clip handed to the transcriber contains the audio spoken after the limit")]
	public void ThenTheClipContainsTheAudioSpokenAfterTheLimit() => driver.AssertClipContainsPostLimitAudio();

	[When(@"the user dictates up to the near-limit threshold")]
	public void WhenTheUserDictatesUpToTheNearLimitThreshold() => driver.DictateToTheNearLimitThreshold();

	[Then(@"a near-limit warning is published")]
	public void ThenANearLimitWarningIsPublished() => driver.AssertNearLimitWarningPublished();

	[When(@"the user keeps dictating past the soft limit")]
	public void WhenTheUserKeepsDictatingPastTheSoftLimit() => driver.KeepDictatingPastTheSoftLimit();

	[Then(@"an at-limit signal is published")]
	public void ThenAnAtLimitSignalIsPublished() => driver.AssertAtLimitSignalPublished();

	[Then(@"the audio spoken past the limit is still retained in the recording")]
	public Task ThenTheAudioSpokenPastTheLimitIsStillRetained() => driver.AssertAudioPastTheLimitIsRetained();

	[Given(@"a recording hard limit of (\d+) ms")]
	public void GivenARecordingHardLimitOfMs(int milliseconds) => driver.ConfigureHardLimit(milliseconds);

	[When(@"the user dictates past the hard limit")]
	public void WhenTheUserDictatesPastTheHardLimit() => driver.DictatePastTheHardLimit();

	[Then(@"the dictation is stopped and the clip is transcribed automatically")]
	public Task ThenTheDictationIsStoppedAndTheClipIsTranscribedAutomatically() => driver.AssertStoppedAndTranscribedAtTheHardLimit();

	[Then(@"a hard-limit stop signal is published")]
	public void ThenAHardLimitStopSignalIsPublished() => driver.AssertHardLimitStopSignalPublished();
}
