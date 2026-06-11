// Thin step definitions for the @WHISPER-103 sidebar-contrast feature. Each step delegates to the
// SidebarThemeDriver (injected by the Reqnroll DI plugin); no logic lives here.

using Dictation.Specs.Drivers;
using Reqnroll;

namespace Dictation.Specs.StepDefinitions;

[Binding]
public sealed class SidebarContrastSteps(SidebarThemeDriver driver)
{
	[Then(@"each nav label colour has at least 4\.5 to 1 contrast against its background")]
	public void ThenLabelsMeetAa() => driver.AssertLabelContrastMeetsAa();

	[Then(@"the selected-item accent has at least 3 to 1 contrast against the sidebar")]
	public void ThenAccentDistinct() => driver.AssertSelectedAccentIsDistinct();

	[Then(@"the shell window's sidebar uses shared brush resources with no hardcoded colour hex")]
	public void ThenSharedBrushesNoHex() => driver.AssertSidebarUsesSharedBrushesNoHex();

	[Then(@"the nav button style defines visible hover, pressed, focus, and selected states")]
	public void ThenStatesDefined() => driver.AssertNavStyleDefinesAllStates();
}
