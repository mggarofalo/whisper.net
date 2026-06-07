// @WHISPER-25 — drives the single-instance scenarios. Steps stay thin; the SingleInstanceDriver
// exercises the real SingleInstanceCoordinator over fake lock + signal seams (modelling the OS-global
// mutex and cross-process activation) and the shared fake shell presenter.

using Dictation.Specs.Drivers;
using Reqnroll;

namespace Dictation.Specs.StepDefinitions;

[Binding]
public sealed class SingleInstanceSteps(SingleInstanceDriver driver)
{
	[Given(@"an instance of the application is already running")]
	public void GivenAnInstanceIsAlreadyRunning() => driver.AnInstanceIsAlreadyRunning();

	[Given(@"a previous instance has shut down gracefully")]
	public void GivenAPreviousInstanceShutDownGracefully() => driver.APreviousInstanceShutDownGracefully();

	[When(@"the user launches the application again")]
	public void WhenTheUserLaunchesAgain() => driver.LaunchAgain();

	[When(@"the user launches the application")]
	public void WhenTheUserLaunches() => driver.LaunchApplication();

	[Then(@"the second process exits without starting a new instance")]
	public void ThenTheSecondProcessExitsWithoutStarting() => driver.AssertSecondExitedWithoutStarting();

	[Then(@"the existing instance is brought to the foreground")]
	public void ThenTheExistingInstanceIsBroughtToForeground() => driver.AssertExistingBroughtToForeground();

	[Then(@"the application starts as the sole instance")]
	public void ThenTheApplicationStartsAsSoleInstance() => driver.AssertStartedAsSoleInstance();
}
