// Thin step definitions for the clipboard-fallback feature. Each step delegates to the
// ClipboardDeliveryDriver (injected by the Reqnroll DI plugin); no logic lives here.

using Dictation.Specs.Drivers;
using Reqnroll;

namespace Dictation.Specs.StepDefinitions;

[Binding]
public sealed class ClipboardFallbackSteps(ClipboardDeliveryDriver driver)
{
	[Given(@"the clipboard contains user content ""(.*)""")]
	public void GivenTheClipboardContainsUserContent(string content) => driver.ClipboardContains(content);

	[Given(@"no other process modifies the clipboard during delivery")]
	public void GivenNoOtherProcessModifiesTheClipboardDuringDelivery()
	{
		// The default: no concurrent-copy hook is arranged.
	}

	[When(@"the text ""(.*)"" is delivered via the clipboard path")]
	public void WhenTheTextIsDeliveredViaTheClipboardPath(string text) => driver.Deliver(text);

	[When(@"another process copies ""(.*)"" before restore occurs")]
	public void WhenAnotherProcessCopiesBeforeRestoreOccurs(string content) =>
		driver.AnotherProcessCopiesDuringDelivery(content);

	[Then(@"the delivered text ""(.*)"" is pasted into the focused window")]
	public void ThenTheDeliveredTextIsPastedIntoTheFocusedWindow(string text) => driver.AssertPasted(text);

	[Then(@"the clipboard again contains ""(.*)""")]
	public void ThenTheClipboardAgainContains(string content) => driver.AssertClipboardContains(content);

	[Then(@"the clipboard still contains ""(.*)""")]
	public void ThenTheClipboardStillContains(string content) => driver.AssertClipboardContains(content);
}
