// Thin step definitions for UIPI-aware delivery. Each step delegates to the
// UipiDeliveryDriver; the "model will transcribe" given is reused from the push-to-talk steps (it
// configures the same scoped transcriber). The unelevated given is scene-setting — the app runs
// unelevated in tests anyway, and the higher-integrity foreground is what drives the UIPI branch.

using Application.Ports;
using Dictation.Specs.Drivers;
using Reqnroll;

namespace Dictation.Specs.StepDefinitions;

[Binding]
public sealed class UipiAwareDeliverySteps(UipiDeliveryDriver driver)
{
	[Given(@"the focused window belongs to a higher-integrity process")]
	public void GivenTheFocusedWindowIsHigherIntegrity() =>
		driver.ForegroundWindowIntegrityIs(ForegroundIntegrity.Higher);

	[Given(@"the focused window belongs to a same-integrity process")]
	public void GivenTheFocusedWindowIsSameIntegrity() =>
		driver.ForegroundWindowIntegrityIs(ForegroundIntegrity.Same);

	[Given(@"the application is running unelevated")]
	public void GivenTheApplicationIsRunningUnelevated()
	{
		// Scene-setting only — the test process is unelevated; the foreground integrity drives the branch.
	}

	[When(@"text delivery is attempted")]
	public Task WhenTextDeliveryIsAttempted() => driver.AttemptDelivery();

	[Then(@"the user is informed delivery was blocked by UIPI")]
	public void ThenTheUserIsInformedDeliveryWasBlockedByUipi() => driver.AssertBlockedByUipi();

	[Then(@"no exception is thrown")]
	public void ThenNoExceptionIsThrown() => driver.AssertCompletedWithoutException();

	[Then(@"the text ""(.*)"" is delivered without a UIPI warning")]
	public void ThenTheTextIsDeliveredWithoutAUipiWarning(string text) =>
		driver.AssertDeliveredWithoutWarning(text);
}
