// Thin step definitions for the output-transforms feature. Each step delegates to the
// OutputTransformDriver (injected by the Reqnroll DI plugin); no logic lives here.

using Dictation.Specs.Drivers;
using Reqnroll;

namespace Dictation.Specs.StepDefinitions;

[Binding]
public sealed class OutputTransformsSteps(OutputTransformDriver driver)
{
	[Given(@"the ""(.*)"" transform is registered")]
	public void GivenTheTransformIsRegistered(string name) => driver.AssertTransformIsRegistered(name);

	[Given(@"no transform named ""(.*)"" is registered")]
	public void GivenNoTransformNamedIsRegistered(string name) => driver.AssertTransformIsNotRegistered(name);

	[Given(@"the rephrase client is available")]
	public void GivenTheRephraseClientIsAvailable() => driver.RephraseClientIsAvailable();

	[Given(@"the rephrase client is disabled")]
	public void GivenTheRephraseClientIsDisabled() => driver.RephraseClientIsDisabled();

	[When(@"I apply ""(.*)"" to ""(.*)""")]
	public Task WhenIApplyTo(string name, string text) => driver.Apply(name, text);

	[Then(@"the rephrase client receives the bullets prompt with that text")]
	public void ThenTheRephraseClientReceivesTheBulletsPrompt() => driver.AssertRephraseReceivedTransformPrompt();

	[Then(@"the rewritten result is returned")]
	public void ThenTheRewrittenResultIsReturned() => driver.AssertRewrittenResultReturned();

	[Then(@"a recoverable ""unknown transform"" error is returned")]
	public void ThenARecoverableUnknownTransformErrorIsReturned() => driver.AssertUnknownTransformError();

	[Then(@"no rephrase call is made")]
	public void ThenNoRephraseCallIsMade() => driver.AssertNoRephraseCall();

	[Then(@"the text ""(.*)"" is returned unchanged")]
	public void ThenTheTextIsReturnedUnchanged(string expected) => driver.AssertReturnedUnchanged(expected);
}
