// Thin step definitions for the @WHISPER-36 transcription-normalization feature. Each step delegates
// to the TranscriptionNormalizationDriver (injected by the Reqnroll DI plugin); no logic lives here.

using Dictation.Specs.Drivers;
using Reqnroll;

namespace Dictation.Specs.StepDefinitions;

[Binding]
public sealed class TranscriptionNormalizationSteps(TranscriptionNormalizationDriver driver)
{
	[Given(@"the ""remove filler words"" setting is on")]
	public void GivenRemoveFillerOn() => driver.SetFillerRemoval(true);

	[Given(@"the ""remove filler words"" setting is off")]
	public void GivenRemoveFillerOff() => driver.SetFillerRemoval(false);

	[Given(@"a raw transcription ""(.*)""")]
	public void GivenARawTranscription(string raw) => driver.SetRawTranscription(raw);

	[When(@"the transcription is normalized")]
	public void WhenTheTranscriptionIsNormalized() => driver.Normalize();

	[Then(@"the normalized text is ""(.*)""")]
	public void ThenTheNormalizedTextIs(string expected) => driver.AssertNormalized(expected);
}
