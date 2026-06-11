// Thin step definitions for the @WHISPER-121 theme-switcher feature. Each step delegates to the
// ThemeSwitcherDriver (injected by the Reqnroll DI plugin); no logic lives here.

using Dictation.Specs.Drivers;
using Reqnroll;

namespace Dictation.Specs.StepDefinitions;

[Binding]
public sealed class ThemeSwitcherSteps(ThemeSwitcherDriver driver)
{
	[Given(@"the theme switcher is loaded showing the system theme")]
	public async Task GivenSwitcherLoaded()
	{
		await driver.LoadSwitcher();
		driver.AssertSwitcherShows("System");
	}

	[When(@"the user selects the ""(.*)"" theme")]
	public void WhenSelectsTheme(string theme) => driver.SelectTheme(theme);

	[Then(@"the dark theme is persisted")]
	public void ThenDarkPersisted() => driver.AssertPersisted("Dark");

	[Then(@"reopening the switcher shows the dark theme")]
	public Task ThenReopenShowsDark() => driver.AssertReopeningShows("Dark");
}
