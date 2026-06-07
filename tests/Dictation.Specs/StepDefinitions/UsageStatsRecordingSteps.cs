// @WHISPER-24 — drives the stats recording + aggregation scenarios. Steps stay thin; the
// UsageStatsRecordingDriver owns HOW the real Mediator pipeline and SQLite store are exercised against a
// private temp-file database.

using Dictation.Specs.Drivers;
using Reqnroll;

namespace Dictation.Specs.StepDefinitions;

[Binding]
public sealed class UsageStatsRecordingSteps(UsageStatsRecordingDriver driver)
{
	[Given(@"no statistics have been recorded")]
	public void GivenNoStatisticsRecorded()
	{
		// A fresh temp database starts empty; nothing to set up.
	}

	[Given(@"I have recorded a transcription of (\d+) seconds with (\d+) characters")]
	[When(@"I record a transcription of (\d+) seconds with (\d+) characters")]
	public Task RecordTranscription(int seconds, int characters) => driver.RecordTranscription(seconds, characters);

	[When(@"the statistics store is reopened")]
	public void WhenTheStatisticsStoreIsReopened() => driver.Restart();

	[Then(@"the total transcription count is (\d+)")]
	public Task ThenTotalTranscriptionCount(int expected) => driver.AssertTranscriptionCount(expected);

	[Then(@"the total audio duration is (\d+) seconds")]
	public Task ThenTotalAudioDuration(int expected) => driver.AssertAudioSeconds(expected);

	[Then(@"the total character count is (\d+)")]
	public Task ThenTotalCharacterCount(int expected) => driver.AssertCharacterCount(expected);
}
