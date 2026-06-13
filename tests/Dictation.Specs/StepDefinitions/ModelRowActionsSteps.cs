// Thin step definitions for the contextual-action feature. Each step delegates to the
// ModelRowActionsDriver (injected by the Reqnroll DI plugin); no logic lives here. The step text is
// distinct from the download steps so Reqnroll binds each unambiguously.

using Dictation.Specs.Drivers;
using Reqnroll;

namespace Dictation.Specs.StepDefinitions;

[Binding]
public sealed class ModelRowActionsSteps(ModelRowActionsDriver driver)
{
	[Given(@"the model rows are loaded with ""(.*)"" active and ""(.*)"" downloaded")]
	public Task GivenLoadedWith(string activeId, string downloadedId) => driver.LoadWith(activeId, downloadedId);

	[Given(@"the model rows are loaded with nothing downloaded")]
	public Task GivenLoadedNothing() => driver.LoadWithNothingDownloaded();

	[When(@"a download is begun on ""(.*)""")]
	public void WhenDownloadBegun(string id) => driver.StartDownload(id);

	[Then(@"the ""(.*)"" row offers only its Download action")]
	public void ThenOnlyDownload(string id) => driver.AssertOnlyDownload(id);

	[Then(@"the ""(.*)"" row offers only its Select action")]
	public void ThenOnlySelect(string id) => driver.AssertOnlySelect(id);

	[Then(@"the ""(.*)"" row offers only its Cancel action")]
	public void ThenOnlyCancel(string id) => driver.AssertOnlyCancel(id);

	[Then(@"the ""(.*)"" row offers no row action and is shown as the selected model")]
	public void ThenNoActionButActive(string id) => driver.AssertNoActionButActive(id);
}
