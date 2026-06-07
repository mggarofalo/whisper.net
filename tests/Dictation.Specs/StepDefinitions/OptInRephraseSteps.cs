// Thin step definitions for the @WHISPER-40 opt-in rephrase feature. Each step delegates to the
// RephraseDriver (injected by the Reqnroll DI plugin); no logic lives here.

using Dictation.Specs.Drivers;
using Reqnroll;

namespace Dictation.Specs.StepDefinitions;

[Binding]
public sealed class OptInRephraseSteps(RephraseDriver driver)
{
	[Given(@"the AI rephrase setting has never been enabled")]
	public void GivenRephraseNeverEnabled() => driver.NeverEnabled();

	[Given(@"AI rephrase is enabled")]
	public void GivenRephraseEnabled() => driver.Enable();

	[Given(@"the configured endpoint host is ""(.*)""")]
	public void GivenTheConfiguredEndpointHostIs(string host) => driver.UseEndpointHost(host);

	[Given(@"AI rephrase is enabled against a local Ollama returning ""(.*)""")]
	public void GivenEnabledAgainstLocalReturning(string responseText) => driver.EnableAgainstLocalReturning(responseText);

	[Given(@"AI rephrase is enabled against a failing local Ollama")]
	public void GivenEnabledAgainstFailingLocal() => driver.EnableAgainstFailingLocal();

	[When(@"text is sent for rephrasing")]
	public Task WhenTextIsSentForRephrasing() => driver.Rephrase("some recognized text");

	[When(@"the text ""(.*)"" is sent for rephrasing")]
	public Task WhenTheTextIsSentForRephrasing(string text) => driver.Rephrase(text);

	[When(@"the rephrase configuration is validated")]
	public void WhenTheRephraseConfigurationIsValidated() => driver.ValidateConfiguration();

	[Then(@"no rephrase request is sent")]
	public void ThenNoRephraseRequestIsSent() => driver.AssertNoNetworkCall();

	[Then(@"a ""rephrase disabled"" result is returned")]
	public void ThenADisabledResultIsReturned() => driver.AssertDisabledResult();

	[Then(@"validation fails with a ""localhost only"" error")]
	public void ThenValidationFailsLocalhostOnly() => driver.AssertValidationFailedLocalhostOnly();

	[Then(@"the request goes to a loopback endpoint")]
	public void ThenTheRequestGoesToLoopback() => driver.AssertRequestWentToLoopback();

	[Then(@"the rewritten text ""(.*)"" is returned")]
	public void ThenTheRewrittenTextIsReturned(string expected) => driver.AssertRephrasedTo(expected);

	[Then(@"the original text ""(.*)"" is returned as a recoverable failure")]
	public void ThenTheOriginalTextIsReturnedAsRecoverableFailure(string expected) => driver.AssertDegradedToOriginal(expected);
}
