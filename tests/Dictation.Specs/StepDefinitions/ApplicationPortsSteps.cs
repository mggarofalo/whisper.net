// Thin step definitions for the Application-ports feature. Each step delegates to the
// ApplicationPortsDriver (injected by the Reqnroll DI plugin); no logic lives here.

using Dictation.Specs.Drivers;
using Reqnroll;

namespace Dictation.Specs.StepDefinitions;

[Binding]
public sealed class ApplicationPortsSteps(ApplicationPortsDriver driver)
{
	[Given(@"the transcriber port is replaced with a substitute that returns ""(.*)""")]
	public void GivenTheTranscriberReturns(string text) => driver.TranscriberReturns(text);

	[When(@"a transcription is requested through the port")]
	public Task WhenATranscriptionIsRequested() => driver.RequestTranscription();

	[Then(@"the caller receives the text ""(.*)""")]
	public void ThenTheCallerReceives(string text) => driver.AssertTranscribed(text);

	[When("the Application port method signatures are inspected")]
	public void WhenThePortSignaturesAreInspected()
	{
		// No-op: the inspection is performed in the assertion step so its result is fresh.
	}

	[Then("no parameter or return type comes from a native or framework dependency")]
	public void ThenNoPortLeaksNativeTypes() => driver.AssertNoPortLeaksNativeOrFrameworkTypes();
}
