// Thin step definitions for the @WHISPER-33 audio/hotkey configuration feature. Each step delegates to
// the AudioHotkeyConfigDriver (injected by the Reqnroll DI plugin); no logic lives here.

using Dictation.Specs.Drivers;
using Reqnroll;

namespace Dictation.Specs.StepDefinitions;

[Binding]
public sealed class AudioHotkeyConfigSteps(AudioHotkeyConfigDriver driver)
{
	private const string DefaultHotkey = "Ctrl+Shift+D";

	[Given(@"the audio settings view shows the available input devices")]
	public async Task GivenTheAudioViewShowsDevices()
	{
		driver.DevicesAvailable("Mic-A", "Mic-B");
		await driver.LoadAudio();
		driver.AssertDevicesListed("Mic-A", "Mic-B");
	}

	[Given(@"the hotkey settings view shows the current binding")]
	public async Task GivenTheHotkeyViewShowsTheCurrentBinding() => await driver.LoadHotkey();

	[When(@"the user selects the ""(.*)"" input device")]
	public async Task WhenTheUserSelectsTheDevice(string id) => await driver.SelectDevice(id);

	[When(@"the user assigns the valid hotkey ""(.*)""")]
	public async Task WhenTheUserAssignsAValidHotkey(string chord) => await driver.AssignHotkey(chord);

	[When(@"the user assigns an empty hotkey")]
	public async Task WhenTheUserAssignsAnEmptyHotkey() => await driver.AssignHotkey(string.Empty);

	[Then(@"an update-settings request is dispatched")]
	public void ThenAnUpdateSettingsRequestIsDispatched() => driver.AssertUpdatePersisted();

	[Then(@"""(.*)"" is still selected when the view is reopened")]
	public async Task ThenTheDeviceIsStillSelectedAfterReopen(string id)
	{
		await driver.ReloadAudio();
		driver.AssertSelectedDeviceIs(id);
	}

	[Then(@"the binding ""(.*)"" is shown after reload")]
	public async Task ThenTheBindingIsShownAfterReload(string chord)
	{
		await driver.ReloadHotkey();
		driver.AssertCurrentHotkeyIs(chord);
	}

	[Then(@"the hotkey change is rejected and surfaced")]
	public void ThenTheHotkeyChangeIsRejected() => driver.AssertHotkeyRejected(DefaultHotkey);

	[Then(@"no settings are written")]
	public void ThenNoSettingsAreWritten() => driver.AssertNothingPersisted();
}
