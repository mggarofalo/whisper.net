// Thin step definitions for the custom-vocabulary feature. The assembly steps delegate to
// VocabularyConditioningDriver; the transcription steps delegate to VocabularyTranscriptionDriver
// (the real WhisperTranscriber over a capturing fake engine). No logic lives here.

using Dictation.Specs.Drivers;
using Reqnroll;

namespace Dictation.Specs.StepDefinitions;

[Binding]
public sealed class CustomVocabularySteps(
	VocabularyConditioningDriver conditioning,
	VocabularyTranscriptionDriver transcription)
{
	[Given(@"a custom vocabulary containing ""(.*)"" and ""(.*)""")]
	public void GivenACustomVocabularyContaining(string first, string second) =>
		conditioning.AddVocabularyTerms(first, second);

	[Given(@"an empty custom vocabulary")]
	public void GivenAnEmptyCustomVocabulary() => conditioning.UseEmptyVocabulary();

	[When(@"transcription decoding options are assembled")]
	public void WhenDecodingOptionsAreAssembled() => conditioning.Assemble();

	[Then(@"the initial prompt includes those terms")]
	public void ThenTheInitialPromptIncludesThoseTerms() => conditioning.AssertInitialPromptIncludesGivenTerms();

	[Then(@"the first-token log-probability threshold is disabled")]
	public void ThenTheFirstTokenThresholdIsDisabled() => conditioning.AssertFirstTokenThresholdDisabled();

	[Then(@"no initial prompt is set")]
	public void ThenNoInitialPromptIsSet() => conditioning.AssertNoInitialPrompt();

	[Then(@"the first-token log-probability threshold retains its default")]
	public void ThenTheFirstTokenThresholdRetainsItsDefault() => conditioning.AssertFirstTokenThresholdDefault();

	[Given(@"a loaded transcriber with the custom vocabulary ""(.*)""")]
	public void GivenALoadedTranscriberWithVocabulary(string term) => transcription.StartWithVocabulary(term);

	[When(@"a clip is transcribed")]
	public Task WhenAClipIsTranscribed() => transcription.Transcribe();

	[When(@"the custom vocabulary changes to ""(.*)""")]
	public void WhenTheVocabularyChangesTo(string term) => transcription.ChangeVocabulary(term);

	[Then(@"the decoder was conditioned with a prompt containing ""(.*)""")]
	public void ThenTheDecoderWasConditionedWith(string term) => transcription.AssertLastPromptContains(term);

	[Then(@"the engine was loaded only once")]
	public void ThenTheEngineWasLoadedOnlyOnce() => transcription.AssertEngineLoadedOnce();
}
