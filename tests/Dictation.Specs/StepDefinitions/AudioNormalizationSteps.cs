// Thin step definitions for the @WHISPER-23 normalization feature. Each step delegates to the
// AudioNormalizationDriver (injected by the Reqnroll DI plugin); no logic lives here.

using Dictation.Specs.Drivers;
using Reqnroll;

namespace Dictation.Specs.StepDefinitions;

[Binding]
public sealed class AudioNormalizationSteps(AudioNormalizationDriver driver)
{
	[Given(@"one second of source audio at (\d+) Hz with (\d+) channel\(s\)")]
	public void GivenOneSecondOfSource(int sampleRate, int channels) => driver.PrepareOneSecondSource(sampleRate, channels);

	[When(@"the audio is normalized")]
	public void WhenTheAudioIsNormalized() => driver.Normalize();

	[Then(@"the result is one second of 16000 Hz mono float audio")]
	public void ThenTheResultIsOneSecondMono16k() => driver.AssertOneSecondMono16k();

	[Given(@"a buffering preroll of (\d+) ms")]
	public void GivenABufferingPreroll(int ms) => driver.ConfigurePreroll(ms);

	[Given(@"(\d+) ms of audio has been captured while idle")]
	public void GivenIdleAudioCaptured(int ms) => driver.CaptureIdle(ms);

	[When(@"recording starts and then stops with no further audio")]
	public void WhenRecordingStartsThenStops() => driver.RecordThenStop();

	[Then(@"the finalized recording is the most recent (\d+) ms of preroll audio")]
	public void ThenRecordingIsRecentPreroll(int ms) => driver.AssertRecordingIsRecentPreroll(ms);

	[Given(@"a maximum recording duration of (\d+) ms")]
	public void GivenAMaximumDuration(int ms) => driver.ConfigureMaxDuration(ms);

	[Given(@"recording has started")]
	public void GivenRecordingHasStarted() => driver.StartRecording();

	[When(@"(\d+) ms of audio is captured")]
	public void WhenAudioIsCaptured(int ms) => driver.CaptureWhileRecording(ms);

	[Then(@"the finalized recording is exactly (\d+) ms long")]
	public void ThenRecordingIsExactlyMsLong(int ms) => driver.AssertRecordingDurationMs(ms);

	[Then(@"the maximum-duration cap is reported to the caller")]
	public void ThenCapReported() => driver.AssertCapReported();
}
