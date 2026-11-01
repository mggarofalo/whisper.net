// Thin step definitions for the selection-list feature. Each step delegates to the
// ModelSelectionListDriver (injected by the Reqnroll DI plugin); no logic lives here.

using Dictation.Specs.Drivers;
using Reqnroll;

namespace Dictation.Specs.StepDefinitions;

[Binding]
public sealed class ModelSelectionListSteps(ModelSelectionListDriver driver)
{
	[Given(@"the model list is loaded with ""(.*)"" in use and ""(.*)"" on this device")]
	public Task GivenLoaded(string activeId, string downloadedId) => driver.LoadWith(activeId, downloadedId);

	[Given(@"switching to ""(.*)"" will fail")]
	public void GivenSwitchFails(string id) => driver.SwitchWillFail(id);

	[When(@"the user chooses ""(.*)"" in the model list while its download is held")]
	public void WhenChooseHeld(string id) => driver.ChooseWhileDownloadHeld(id);

	[When(@"the user chooses ""(.*)"" in the model list")]
	public Task WhenChoose(string id) => driver.Choose(id);

	[When(@"the held download finishes")]
	public Task WhenReleased() => driver.ReleaseDownload();

	[When(@"the user downloads ""(.*)"" from its row")]
	public Task WhenDownloadFromRow(string id) => driver.DownloadFromRow(id);

	[Then(@"the ""(.*)"" row reads ""(.*)""")]
	public void ThenStatus(string id, string status) => driver.AssertStatus(id, status);

	[Then(@"every row's caption shows its id, download size and status")]
	public void ThenCaptions() => driver.AssertCaptionsDescribeEachRow();

	[Then(@"the model list marks ""(.*)"" as in use")]
	[Then(@"the model list still marks ""(.*)"" as in use")]
	public void ThenInUse(string id) => driver.AssertInUse(id);

	[Then(@"the ""(.*)"" row explains that it couldn't switch")]
	public void ThenSwitchFailure(string id) => driver.AssertSwitchFailureExplained(id);

	[Then(@"no model switch is requested")]
	public void ThenNoSwitch() => driver.AssertNoSwitchRequested();
}
