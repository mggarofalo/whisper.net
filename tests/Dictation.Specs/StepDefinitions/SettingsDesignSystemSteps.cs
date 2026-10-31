// Thin step definitions for the settings design-system and write-isolation features (WHISPER-142). Each
// step delegates to its driver (injected by the Reqnroll DI plugin); no logic lives here.

using Dictation.Specs.Drivers;
using Reqnroll;

namespace Dictation.Specs.StepDefinitions;

[Binding]
public sealed class SettingsDesignSystemSteps(SettingsDesignSystemDriver design, SettingsWriteIsolationDriver isolation)
{
	[Then(@"no settings page declares its own colour literal")]
	public void ThenNoColourLiterals() => design.AssertViewsHoldNoColourLiterals();

	[Then(@"status and surface colours are Fluent semantic brushes bound live to the theme")]
	public void ThenSemanticBrushes() => design.AssertStatusColoursAreSemanticBrushes();

	[Then(@"every settings page takes its type sizes and spacing from the shared tokens")]
	public void ThenSharedTypeScale() => design.AssertViewsUseTheSharedTypeScale();

	[Then(@"secondary text uses the secondary text brush rather than reduced opacity")]
	public void ThenSecondaryBrush() => design.AssertSecondaryTextUsesTheSecondaryBrush();

	[Then(@"every settings page title matches its navigation label")]
	public void ThenTitlesMatchNav() => design.AssertPageTitlesMatchNavLabels();

	[Then(@"no page title shares a toolbar row with its commands")]
	public void ThenTitleRowClear() => design.AssertPageCommandsLeaveTheTitleRow();

	[Then(@"the History page offers ""Load more"" below its list")]
	public void ThenLoadMoreAtBottom() => design.AssertPageCommandsLeaveTheTitleRow();

	[Then(@"the scrolling settings pages reserve a gutter for the scrollbar")]
	public void ThenScrollbarGutter() => design.AssertScrollingPagesReserveTheScrollbarGutter();

	[Then(@"the settings window declares a minimum width and height")]
	public void ThenWindowMinSize() => design.AssertWindowHasAMinimumSize();

	[Then(@"the History, Home and Model list rows expose their content as the automation name")]
	public void ThenRowAutomationNames() => design.AssertListRowsHaveMeaningfulAutomationNames();

	[Given(@"the persisted active model is ""(.*)""")]
	public void GivenActiveModel(string modelId) => isolation.GivenActiveModel(modelId);

	[Given(@"the ""(.*)"" settings section is open")]
	public Task GivenSectionOpen(string section) => isolation.OpenSection(section);

	[When(@"the active model is switched to ""(.*)"" elsewhere in the app")]
	public Task WhenModelSwitchedElsewhere(string modelId) => isolation.SwitchModelElsewhere(modelId);

	[When(@"the user changes the ""(.*)"" section's setting")]
	public Task WhenSectionChanged(string _) => isolation.MakeSectionChange();

	[Then(@"the ""(.*)"" section's change is persisted")]
	public void ThenSectionChangePersisted(string section) => isolation.AssertSectionChangePersisted(section);

	[Then(@"the persisted active model is still ""(.*)""")]
	public void ThenModelStill(string modelId) => isolation.AssertPersistedModel(modelId);

	[Then(@"Home and Model agree that ""(.*)"" is the active model")]
	public Task ThenHomeAndModelAgree(string modelId) => isolation.AssertHomeAndModelAgree(modelId);
}
