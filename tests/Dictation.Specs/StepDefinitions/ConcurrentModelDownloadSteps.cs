// Thin step definitions for the @WHISPER-107 concurrent-download feature. Each step delegates to the
// ConcurrentModelDownloadDriver (injected by the Reqnroll DI plugin); no logic lives here. The step text
// is deliberately distinct from the @WHISPER-81 single-download steps so Reqnroll binds each
// unambiguously.

using Dictation.Specs.Drivers;
using Reqnroll;

namespace Dictation.Specs.StepDefinitions;

[Binding]
public sealed class ConcurrentModelDownloadSteps(ConcurrentModelDownloadDriver driver)
{
	[Given(@"the model picker is loaded for concurrent downloads")]
	public Task GivenTheModelPickerIsLoaded() => driver.LoadList();

	[When(@"the user begins downloading ""(.*)""")]
	public void WhenTheUserBeginsDownloading(string id) => driver.StartDownload(id);

	[When(@"the user cancels the ""(.*)"" download")]
	public void WhenTheUserCancels(string id) => driver.Cancel(id);

	[Then(@"the ""(.*)"" download is running with its own progress")]
	public void ThenTheDownloadIsRunning(string id) => driver.AssertRunningWithProgress(id);

	[Then(@"the ""(.*)"" download can be cancelled on its own row")]
	public void ThenTheDownloadIsCancellable(string id) => driver.AssertCancellable(id);

	[Then(@"the ""(.*)"" row can still start its own download")]
	public void ThenTheRowCanStartDownload(string id) => driver.AssertRowCanStartDownload(id);

	[Then(@"the ""(.*)"" download has reset")]
	public async Task ThenTheDownloadHasReset(string id)
	{
		await driver.AwaitDownload(id);
		driver.AssertReset(id);
	}
}
