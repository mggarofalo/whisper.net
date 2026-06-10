// Thin step definitions for the @WHISPER-93 declarative-event-wiring feature. Each step delegates to
// the DeclarativeWiringDriver (injected by the Reqnroll DI plugin); no logic lives here.

using Dictation.Specs.Drivers;
using Reqnroll;

namespace Dictation.Specs.StepDefinitions;

[Binding]
public sealed class DeclarativeWiringSteps(DeclarativeWiringDriver driver)
{
	[Then(@"no view outside the input controls wires events in markup or code-behind")]
	public void ThenNoViewOutsideTheInputControlsWiresEvents() => driver.AssertNoEventWiringOutsideInputControls();

	[Then(@"a reusable focus-on-activate behavior exists")]
	public void ThenAReusableFocusOnActivateBehaviorExists() => driver.AssertFocusBehaviorExists();

	[Then(@"at least one feature view applies it through interaction behaviors")]
	public void ThenAtLeastOneFeatureViewAppliesIt() => driver.AssertFocusBehaviorAppliedDeclaratively();

	[Then(@"no view carries a per-view loaded handler")]
	public void ThenNoViewCarriesAPerViewLoadedHandler() => driver.AssertNoPerViewLoadedHandler();

	[Then(@"the presentation project references the xaml behaviors library")]
	public void ThenThePresentationProjectReferencesTheXamlBehaviorsLibrary() => driver.AssertBehaviorsLibraryReferenced();

	[Then(@"the architecture guide records the behavior-versus-command guideline")]
	public void ThenTheArchitectureGuideRecordsTheGuideline() => driver.AssertGuidelineDocumented();
}
