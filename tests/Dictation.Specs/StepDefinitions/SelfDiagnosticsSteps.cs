// Thin step definitions for the @WHISPER-50 self-diagnostics feature. Each step delegates to the
// DiagnosticsDriver (injected by the Reqnroll DI plugin); no logic lives here.

using Dictation.Specs.Drivers;
using Reqnroll;

namespace Dictation.Specs.StepDefinitions;

[Binding]
public sealed class SelfDiagnosticsSteps(DiagnosticsDriver driver)
{
	[Given(@"a capture device is available")]
	public void GivenACaptureDeviceIsAvailable() => driver.CaptureDeviceAvailable();

	[Given(@"the configured model is downloaded")]
	public void GivenTheConfiguredModelIsDownloaded() => driver.ModelDownloaded();

	[Given(@"the input permission required for the hotkey is granted")]
	public void GivenTheHotkeyPermissionIsGranted() => driver.HotkeyPermissionGranted();

	[Given(@"a Vulkan GPU runtime is available")]
	public void GivenAVulkanRuntimeIsAvailable() => driver.VulkanAvailable();

	[Given(@"every subsystem is healthy")]
	public void GivenEverySubsystemIsHealthy() => driver.Healthy();

	[Given(@"the ""(.*)"" subsystem is unavailable")]
	public void GivenTheSubsystemIsUnavailable(string subsystem) => driver.SubsystemUnavailable(subsystem);

	[Given(@"no Vulkan GPU runtime is available")]
	public void GivenNoVulkanRuntimeIsAvailable() => driver.SubsystemUnavailable("GPU");

	[When(@"the diagnostics run")]
	public async Task WhenTheDiagnosticsRun() => await driver.RunDiagnostics();

	[Then(@"every diagnostic reports a passing status")]
	public void ThenEveryDiagnosticPasses() => driver.AssertEveryCheckPasses();

	[Then(@"the ""(.*)"" check reports a failing status")]
	public void ThenTheCheckFails(string name) => driver.AssertCheckFails(name);

	[Then(@"the ""(.*)"" check does not report a failing status")]
	public void ThenTheCheckDoesNotFail(string name) => driver.AssertCheckDoesNotFail(name);

	[Then(@"every subsystem still produces a result")]
	public void ThenEverySubsystemStillProducesAResult() => driver.AssertEverySubsystemProducedAResult();

	[Then(@"the ""(.*)"" check detail mentions the CPU backend")]
	public void ThenTheCheckDetailMentionsCpu(string name) => driver.AssertCheckDetailContains(name, "CPU");

	[Then(@"each diagnostic has a name, a status, and a non-empty detail")]
	public void ThenEachDiagnosticIsStructured() => driver.AssertEveryResultIsStructured();
}
