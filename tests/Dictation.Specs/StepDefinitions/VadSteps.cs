// Thin step definitions for the @WHISPER-31 voice-activity feature. Each step delegates to the
// VadDriver (injected by the Reqnroll DI plugin); no logic lives here.

using Dictation.Specs.Drivers;
using Reqnroll;

namespace Dictation.Specs.StepDefinitions;

[Binding]
public sealed class VadSteps(VadDriver driver)
{
	[Given(@"a recording containing only silence")]
	public void GivenOnlySilence() => driver.AddSilenceSeconds(2);

	[Given(@"a recording of (\d+) seconds? of speech followed by (\d+) seconds? of silence")]
	public void GivenSpeechThenSilence(int speechSeconds, int silenceSeconds)
	{
		driver.AddSpeechSeconds(speechSeconds);
		driver.AddSilenceSeconds(silenceSeconds);
	}

	[Given(@"a recording of (\d+) seconds? of speech, a (\d+) seconds? pause, then (\d+) seconds? of speech")]
	public void GivenSpeechPauseSpeech(int firstSpeech, int pause, int secondSpeech)
	{
		driver.AddSpeechSeconds(firstSpeech);
		driver.AddSilenceSeconds(pause);
		driver.AddSpeechSeconds(secondSpeech);
	}

	[Given(@"a recording of speech, silence, then speech")]
	public void GivenSpeechSilenceSpeech()
	{
		driver.AddSpeechSeconds(1);
		driver.AddSilenceSeconds(1);
		driver.AddSpeechSeconds(1);
	}

	[Given(@"the mid-silence collapse threshold is (\d+) seconds?")]
	public void GivenMidCollapseThreshold(int seconds) => driver.SetMidCollapseSeconds(seconds);

	[When(@"the recording is gated and trimmed by voice activity")]
	public Task WhenGatedAndTrimmed() => driver.Analyze();

	[When(@"the recording is analyzed for voice activity")]
	public Task WhenAnalyzed() => driver.Analyze();

	[Then(@"the recording is gated out as containing no speech")]
	public void ThenGatedOut() => driver.AssertGatedOut();

	[Then(@"the trimmed recording is (\d+) seconds? long")]
	public void ThenTrimmedSecondsLong(int seconds) => driver.AssertTrimmedSeconds(seconds);

	[Then(@"the speech is preserved")]
	public void ThenSpeechPreserved() => driver.AssertSpeechPreserved();

	[Then(@"both speech portions are preserved")]
	public void ThenBothSpeechPreserved() => driver.AssertSpeechPreserved();

	[Then(@"voice activity is detected in the first and third windows but not the second")]
	public void ThenSpeechInFirstAndThird() => driver.AssertSpeechWindowsAre(0, 2);
}
