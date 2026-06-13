// Thin step definitions for the model-download feature. Each step delegates to the
// ModelDownloadDriver (injected by the Reqnroll DI plugin); no logic lives here.

using Dictation.Specs.Drivers;
using Reqnroll;

namespace Dictation.Specs.StepDefinitions;

[Binding]
public sealed class ModelDownloadSteps(ModelDownloadDriver driver)
{
	[Given(@"the model list is loaded for download")]
	public Task GivenTheModelListIsLoaded() => driver.LoadList();

	[Given(@"the model ""(.*)"" downloads slowly")]
	public void GivenTheModelDownloadsSlowly(string id) => driver.ConfigureGatedDownload(id);

	[Given(@"the model ""(.*)"" download will fail")]
	public void GivenTheModelDownloadWillFail(string id) => driver.ConfigureFailingDownload();

	[When(@"the user starts downloading ""(.*)""")]
	public void WhenTheUserStartsDownloading(string id) => driver.StartDownload(id);

	[When(@"the user cancels the download")]
	public void WhenTheUserCancels() => driver.Cancel();

	[When(@"the user downloads ""(.*)"" to completion")]
	public Task WhenTheUserDownloadsToCompletion(string id) => driver.DownloadToCompletion(id);

	[Then(@"the download for ""(.*)"" is running with progress")]
	public void ThenTheDownloadIsRunning(string id) => driver.AssertRunningWithProgress(id);

	[Then(@"the download for ""(.*)"" is reset and the model is not activated")]
	public async Task ThenTheDownloadIsReset(string id)
	{
		await driver.AwaitDownload();
		driver.AssertResetAndInactive(id);
	}

	[Then(@"a native download error is shown")]
	public void ThenANativeErrorIsShown() => driver.AssertNativeErrorShown();

	[Then(@"the model ""(.*)"" is not activated")]
	public void ThenTheModelIsNotActivated(string id) => driver.AssertNotActivated(id);
}
