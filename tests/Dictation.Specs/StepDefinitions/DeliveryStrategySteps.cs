// Thin step definitions for delivery-strategy selection (@WHISPER-8). Each step delegates to the
// DeliveryStrategyDriver; the "model will transcribe" given is reused from the push-to-talk steps so
// there is something to deliver.

using Dictation.Specs.Drivers;
using Domain.Settings;
using Reqnroll;

namespace Dictation.Specs.StepDefinitions;

[Binding]
public sealed class DeliveryStrategySteps(DeliveryStrategyDriver driver)
{
	[Given(@"the configured delivery strategy is ""(.*)""")]
	public void GivenTheConfiguredDeliveryStrategyIs(string strategy) =>
		driver.ConfiguredDefaultIs(Parse(strategy));

	[Given(@"no per-delivery override is supplied")]
	public void GivenNoPerDeliveryOverrideIsSupplied()
	{
		// The default: no override is arranged.
	}

	[Given(@"a per-delivery override of ""(.*)"" is supplied")]
	public void GivenAPerDeliveryOverrideIsSupplied(string strategy) => driver.OverrideWith(Parse(strategy));

	[When(@"a transcription is delivered")]
	public Task WhenATranscriptionIsDelivered() => driver.Deliver();

	[Then(@"the ""(.*)"" delivery path is used")]
	public void ThenTheDeliveryPathIsUsed(string strategy) => driver.AssertPathUsed(Parse(strategy));

	private static DeliveryStrategy Parse(string strategy) => Enum.Parse<DeliveryStrategy>(strategy, ignoreCase: true);
}
