// @WHISPER-18 — drives the tray icon/menu scenarios. Steps stay thin; the TrayDriver exercises the real
// TrayController over the real recording state machine, with the shell-presenter and host-lifetime
// seams faked.

using Dictation.Specs.Drivers;
using Reqnroll;

namespace Dictation.Specs.StepDefinitions;

[Binding]
public sealed class TrayIconSteps(TrayDriver driver)
{
	[Given(@"the application is running in the tray")]
	[Given(@"the tray context menu is open")]
	public void GivenTheTrayIsActive() => driver.TrayIsActive();

	[When(@"the dictation status changes to recording")]
	public void WhenTheStatusChangesToRecording() => driver.StatusChangesToRecording();

	[When(@"the user selects ""Quit""")]
	public void WhenTheUserSelectsQuit() => driver.SelectQuit();

	[When(@"the user selects ""Open Settings""")]
	public void WhenTheUserSelectsOpenSettings() => driver.SelectOpenSettings();

	[Then(@"the tray icon updates to the recording indicator")]
	public void ThenTheTrayIconUpdatesToRecording() => driver.AssertRecordingIndicator();

	[Then(@"the tray tooltip describes the recording status")]
	public void ThenTheTooltipDescribesRecording() => driver.AssertTooltipDescribesRecording();

	[Then(@"the application shuts down gracefully")]
	public void ThenTheApplicationShutsDownGracefully() => driver.AssertGracefulShutdownRequested();

	[Then(@"the settings window is shown")]
	public void ThenTheSettingsWindowIsShown() => driver.AssertSettingsShown();
}
