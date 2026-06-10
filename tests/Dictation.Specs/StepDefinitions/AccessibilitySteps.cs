// Thin step definitions for the @WHISPER-83 accessibility feature. Each step delegates to the
// AccessibilityDriver (injected by the Reqnroll DI plugin), which inspects the settings view XAML directly.

using Dictation.Specs.Drivers;
using Reqnroll;

namespace Dictation.Specs.StepDefinitions;

[Binding]
public sealed class AccessibilitySteps(AccessibilityDriver driver)
{
	[Given(@"the settings views")]
	public void GivenTheSettingsViews()
	{
		// The settings view XAML is inspected by the driver; nothing to set up.
	}

	[Then(@"the hotkey controls have automation names")]
	public void ThenHotkeyControlsHaveAutomationNames() => driver.AssertHotkeyControlsHaveAutomationNames();

	[Then(@"the device picker controls have automation names")]
	public void ThenDeviceControlsHaveAutomationNames() => driver.AssertDeviceControlsHaveAutomationNames();

	[Then(@"the model picker controls have automation names")]
	public void ThenModelControlsHaveAutomationNames() => driver.AssertModelControlsHaveAutomationNames();

	[Then(@"the settings views declare a keyboard tab order")]
	public void ThenSettingsViewsDeclareTabOrder() => driver.AssertSettingsViewsDeclareTabOrder();
}
