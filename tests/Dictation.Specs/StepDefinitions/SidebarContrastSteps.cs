// Thin step definitions for the navigation-sidebar theming feature. Each step
// delegates to the SidebarThemeDriver (injected by the Reqnroll DI plugin); no logic lives here.

using Dictation.Specs.Drivers;
using Reqnroll;

namespace Dictation.Specs.StepDefinitions;

[Binding]
public sealed class SidebarContrastSteps(SidebarThemeDriver driver)
{
	[Then(@"the nav labels inherit the theme foreground rather than a fixed colour")]
	public void ThenLabelsInheritTheme() => driver.AssertLabelsInheritTheTheme();

	[Then(@"the sidebar surface is a theme-neutral overlay, not a fixed dark panel")]
	public void ThenSurfaceIsNeutral() => driver.AssertSidebarSurfaceIsThemeNeutral();

	[Then(@"the selected nav tab is painted with the system accent")]
	public void ThenSelectedUsesAccent() => driver.AssertSelectedTabUsesTheSystemAccent();

	[Then(@"the selected nav label uses the on-accent text colour")]
	public void ThenSelectedLabelOnAccent() => driver.AssertSelectedLabelUsesOnAccentText();

	[Then(@"the shell window's sidebar uses shared brush resources with no hardcoded colour hex")]
	public void ThenSharedBrushesNoHex() => driver.AssertSidebarUsesSharedBrushesNoHex();

	[Then(@"the nav button style defines visible hover, pressed, focus, and selected states")]
	public void ThenStatesDefined() => driver.AssertNavStyleDefinesAllStates();
}
