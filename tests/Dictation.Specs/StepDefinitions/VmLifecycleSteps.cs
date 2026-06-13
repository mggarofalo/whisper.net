// Thin step definitions for the activation-lifecycle feature. Each step delegates to the
// VmLifecycleDriver (injected by the Reqnroll DI plugin); no logic lives here.

using Dictation.Specs.Drivers;
using Reqnroll;

namespace Dictation.Specs.StepDefinitions;

[Binding]
public sealed class VmLifecycleSteps(VmLifecycleDriver driver)
{
	[Given(@"the shell is open on the ""(.*)"" section")]
	public void GivenTheShellIsOpenOnTheSection(string section) => driver.OpenShellOn(section);

	[Given(@"the user navigates away to the ""(.*)"" section")]
	public void GivenTheUserNavigatesAwayToTheSection(string section) => driver.NavigateTo(section);

	[Given(@"the user navigates back to the ""(.*)"" section")]
	public void GivenTheUserNavigatesBackToTheSection(string section) => driver.NavigateTo(section);

	[When(@"a settings change with hotkey ""(.*)"" is published")]
	public void WhenASettingsChangeWithHotkeyIsPublished(string chord) => driver.PublishSettingsChange(chord);

	[Then(@"the hotkey section shows ""(.*)"" as the current hotkey")]
	public void ThenTheHotkeySectionShowsAsTheCurrentHotkey(string chord) => driver.AssertCurrentHotkeyShown(chord);

	[Then(@"the cached hotkey section does not react to the change")]
	public void ThenTheCachedHotkeySectionDoesNotReact() => driver.AssertHotkeyDidNotReact();

	[Then(@"the architecture guide records the activation lifecycle rule")]
	public void ThenTheArchitectureGuideRecordsTheActivationLifecycleRule() => driver.AssertLifecycleDocumented();
}
