// Thin bindings for the model registry/cache/download feature: each step delegates to
// ModelLibraryDriver, which runs the real catalog, cache, and downloader over a hermetic byte source.

using Dictation.Specs.Drivers;
using Reqnroll;

namespace Dictation.Specs.StepDefinitions;

[Binding]
public sealed class ModelLibrarySteps(ModelLibraryDriver driver)
{
	[Given(@"the ""(.*)"" model is already present in the cache")]
	public void GivenCached(string model) => driver.GivenModelAlreadyCached(model);

	[Given(@"the ""(.*)"" model is not present in the cache")]
	public void GivenNotCached(string model) => driver.GivenModelNotCached(model);

	[When(@"the model's cache status is queried")]
	public void WhenQueryCache() => driver.QueryCacheStatus();

	[When(@"the user requests its download")]
	public Task WhenRequestDownload() => driver.RequestDownload();

	[Then(@"the model is reported as available")]
	public void ThenAvailable() => driver.AssertReportedAvailable();

	[Then(@"no network request is made")]
	public void ThenNoNetwork() => driver.AssertNoNetworkRequest();

	[Then(@"progress is reported until completion")]
	public void ThenProgress() => driver.AssertProgressReportedUntilCompletion();

	[Then(@"the verified model file is present in the cache afterward")]
	public void ThenFileInCache() => driver.AssertVerifiedFileInCache();
}
