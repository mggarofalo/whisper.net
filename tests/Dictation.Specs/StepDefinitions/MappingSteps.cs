// Thin step definitions for the mapping feature. Each step delegates to the MappingDriver (injected by
// the Reqnroll DI plugin); no logic lives here.

using Dictation.Specs.Drivers;
using Reqnroll;

namespace Dictation.Specs.StepDefinitions;

[Binding]
public sealed class MappingSteps(MappingDriver driver)
{
	[Given("a transcript entry domain object")]
	public void GivenATranscriptEntry() => driver.GivenATranscriptEntry();

	[Given("an app-settings domain object")]
	public void GivenAppSettings() => driver.GivenAppSettings();

	[When("it is mapped to a DTO and back to the domain type")]
	public void WhenItIsRoundTripped() => driver.RoundTrip();

	[Then("the round-tripped value equals the original")]
	public void ThenTheRoundTripEqualsTheOriginal() => driver.AssertRoundTripEqualsOriginal();
}
