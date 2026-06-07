// Thin bindings for the GPU contact point feature: each step delegates to GpuBackendDriver, which
// holds the mechanics of faking the probe and running the real backend selector.

using Dictation.Specs.Drivers;
using Reqnroll;

namespace Dictation.Specs.StepDefinitions;

[Binding]
public sealed class GpuBackendSteps(GpuBackendDriver driver)
{
	[Given(@"a usable Vulkan runtime is available")]
	public void GivenVulkanAvailable() => driver.GivenVulkanAvailable();

	[Given(@"no usable Vulkan runtime is available")]
	public void GivenVulkanUnavailable() => driver.GivenVulkanUnavailable();

	[Given(@"probing for a Vulkan runtime fails")]
	public void GivenProbeThrows() => driver.GivenProbeThrows();

	[When(@"the GPU contact point selects a backend")]
	public Task WhenSelectBackend() => driver.SelectBackend();

	[Then(@"the Vulkan GPU backend is chosen")]
	public void ThenGpuChosen() => driver.AssertGpuBackendChosen();

	[Then(@"the CPU backend is chosen")]
	public void ThenCpuChosen() => driver.AssertCpuBackendChosen();

	[Then(@"the selection reason cites Vulkan availability")]
	public void ThenReasonCitesVulkan() => driver.AssertReasonCitesVulkan();

	[Then(@"the application continues without hanging or crashing")]
	public void ThenNoCrash() => driver.AssertCompletedWithoutCrashing();
}
