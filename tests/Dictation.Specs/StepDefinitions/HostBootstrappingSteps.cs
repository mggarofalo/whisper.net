// @WHISPER-12 — drives the Generic Host bootstrap scenarios. Steps stay thin; the AppLifecycleDriver
// owns HOW a real host is composed, launched, and shut down, and asserts the hosted-service lifecycle
// at the host boundary.

using Dictation.Specs.Drivers;
using Reqnroll;

namespace Dictation.Specs.StepDefinitions;

[Binding]
public sealed class HostBootstrappingSteps(AppLifecycleDriver driver)
{
	[Given(@"the application host is composed with its background components")]
	public void GivenTheApplicationHostIsComposed() => driver.ComposeHost();

	[Given(@"the application has been launched")]
	[When(@"the application is launched")]
	public Task WhenTheApplicationIsLaunched() => driver.LaunchAsync();

	[When(@"application shutdown is requested")]
	public Task WhenApplicationShutdownIsRequested() => driver.RequestShutdownAsync();

	[Then(@"every hosted service has been started")]
	public void ThenEveryHostedServiceHasBeenStarted() => driver.AssertEveryHostedServiceStarted();

	[Then(@"the global hotkey listener is observing")]
	public void ThenTheGlobalHotkeyListenerIsObserving() => driver.AssertHotkeyListenerObserving();

	[Then(@"the application is running tray-resident with no window shown")]
	public void ThenTheApplicationIsRunningTrayResident() => driver.AssertRunningWithNoWindowShown();

	[Then(@"every hosted service has been stopped before the host exits")]
	public void ThenEveryHostedServiceHasBeenStoppedBeforeExit() => driver.AssertEveryHostedServiceStoppedBeforeExit();

	[Then(@"the global hotkey listener has stopped observing")]
	public void ThenTheGlobalHotkeyListenerHasStoppedObserving() => driver.AssertHotkeyListenerStopped();
}
