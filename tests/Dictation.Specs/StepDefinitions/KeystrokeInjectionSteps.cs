// Thin step definitions for the universal keystroke-delivery feature. Each step delegates
// to the TextInjectionDriver (injected by the Reqnroll DI plugin); no logic lives here. The focus
// givens are intentionally empty: they set the scene a reader needs, but typing delivery behaves the
// same regardless of which window has focus, which is the whole point of the feature.

using Dictation.Specs.Drivers;
using Reqnroll;

namespace Dictation.Specs.StepDefinitions;

[Binding]
public sealed class KeystrokeInjectionSteps(TextInjectionDriver driver)
{
	[Given(@"a window has keyboard focus")]
	public void GivenAWindowHasKeyboardFocus()
	{
		// Scene-setting only — no state to arrange.
	}

	[Given(@"a terminal that ignores the standard paste shortcut has focus")]
	public void GivenATerminalThatIgnoresPasteHasFocus()
	{
		// Scene-setting only — typing delivery does not depend on the target honoring paste.
	}

	[Given(@"a transcription result ""(.*)"" is ready for delivery")]
	public void GivenATranscriptionResultIsReadyForDelivery(string text) => driver.ResultIsReady(text);

	[When(@"the text injector delivers the result")]
	public void WhenTheTextInjectorDeliversTheResult() => driver.DeliverResult();

	[Then(@"the focused window receives the exact characters ""(.*)""")]
	public void ThenTheFocusedWindowReceivesTheExactCharacters(string text) =>
		driver.AssertFocusedFieldReceived(text);
}
