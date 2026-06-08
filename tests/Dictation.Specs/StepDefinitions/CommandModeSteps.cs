// Thin step definitions for the @WHISPER-35 command-mode hook feature. Each step delegates to the
// CommandModeDriver (injected by the Reqnroll DI plugin); no logic lives here.

using Dictation.Specs.Drivers;
using Reqnroll;

namespace Dictation.Specs.StepDefinitions;

[Binding]
public sealed class CommandModeSteps(CommandModeDriver driver)
{
	[Given(@"a command matcher that recognizes the transcript as a command")]
	public void GivenACommandMatcherThatRecognizesTheTranscriptAsACommand() => driver.MatcherRecognizesCommand();

	[Given(@"a command matcher that recognizes no command")]
	public void GivenACommandMatcherThatRecognizesNoCommand() => driver.MatcherRecognizesNoCommand();

	[When(@"an utterance is transcribed")]
	public Task WhenAnUtteranceIsTranscribed() => driver.TranscribeUtterance();

	[Then(@"the command branch is invoked")]
	public void ThenTheCommandBranchIsInvoked() => driver.AssertCommandBranchInvoked();

	[Then(@"the transcript is not delivered as text")]
	public void ThenTheTranscriptIsNotDeliveredAsText() => driver.AssertNotDeliveredAsText();

	[Then(@"the transcript is delivered as text")]
	public void ThenTheTranscriptIsDeliveredAsText() => driver.AssertDeliveredAsText();
}
