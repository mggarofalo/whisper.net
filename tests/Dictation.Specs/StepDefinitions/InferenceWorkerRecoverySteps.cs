using Dictation.Specs.Drivers;
using Reqnroll;

namespace Dictation.Specs.StepDefinitions;

[Binding]
public sealed class InferenceWorkerRecoverySteps(InferenceWorkerRecoveryDriver driver)
{
	[Given("a warmed GPU inference worker")]
	public Task GivenWarmedGpuWorker()
	{
		driver.GivenCrashedGpuThenSuccessfulGpu();
		return driver.WarmAndArmCrash();
	}

	[Given("the inference worker becomes unavailable during transcription")]
	public void GivenWorkerUnavailable() { }

	[Given("GPU inference becomes unavailable during transcription")]
	public void GivenGpuUnavailable() => driver.GivenGpuFailsTwiceThenCpuSucceeds();

	[Given("a fresh GPU inference worker also cannot complete inference")]
	public void GivenFreshGpuAlsoFails() { }

	[Given("GPU recovery and CPU fallback both fail")]
	public void GivenAllRecoveryFails() => driver.GivenEveryRecoveryFails();

	[Given("a healthy warmed GPU inference worker")]
	public void GivenHealthyWorker() => driver.GivenHealthyWarmedGpu();

	[Given("the tray application owns a running inference worker")]
	public void GivenRunningWorker() => driver.GivenRunningWorker();

	[When("dictation is requested through the worker supervisor")]
	public Task WhenDictationRequested() => driver.RequestDictation();

	[When("two dictations are requested through the worker supervisor")]
	public Task WhenTwoDictationsRequested() => driver.RequestTwoDictations();

	[When("the worker supervisor shuts down")]
	public Task WhenSupervisorShutsDown() => driver.StartThenShutdown();

	[Then("a fresh GPU inference worker retries the dictation")]
	public void ThenFreshGpuRetries() => driver.AssertFreshGpuRetry();

	[Then("a CPU-only inference worker retries the dictation")]
	public void ThenCpuRetries() => driver.AssertCpuRetry();

	[Then("the transcription result is returned")]
	public void ThenResultReturned() => driver.AssertResultReturned();

	[Then("the tray-side supervisor remains available")]
	public void ThenSupervisorAvailable() => driver.AssertSupervisorAvailable();

	[Then("an inference-unavailable error is returned")]
	public void ThenTypedFailure() => driver.AssertTypedFailure();

	[Then("no further worker restart is attempted for that request")]
	public void ThenRecoveryBounded() => driver.AssertRecoveryBounded();

	[Then("both transcription results are returned by the same worker generation")]
	public void ThenSameGeneration() => driver.AssertSameGeneration();

	[Then("the inference worker exits")]
	public void ThenWorkerExits() => driver.AssertWorkerExited();

	[Then("no owned inference worker remains")]
	public void ThenNoWorkerRemains() => driver.AssertNoOwnedWorker();
}
