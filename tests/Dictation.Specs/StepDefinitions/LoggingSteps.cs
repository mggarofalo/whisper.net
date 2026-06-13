// Thin step definitions for the diagnosable-logging feature. Each step delegates to the
// LoggingDriver (injected by the Reqnroll DI plugin); no logic lives here.

using Dictation.Specs.Drivers;
using Reqnroll;

namespace Dictation.Specs.StepDefinitions;

[Binding]
public sealed class LoggingSteps(LoggingDriver driver)
{
	[Given(@"logging is configured for a per-user application-data logs directory")]
	public void GivenLoggingConfiguredForLogsDirectory() => driver.ConfigureLoggingToATempDirectory();

	[When(@"the application logs an informational event")]
	public void WhenApplicationLogsEvent() => driver.LogAnInformationalEvent();

	[Then(@"the event is written to a rolling log file in that directory")]
	public void ThenEventWrittenToLogFile() => driver.AssertEventWrittenToRollingLogFile();
}
