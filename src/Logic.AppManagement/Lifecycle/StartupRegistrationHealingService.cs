// Keeps the launch-at-login registration pointing at THIS installation, and logs its state on every
// start so a failure to launch at login is diagnosable from the log rather than by guesswork.
//
// Why this service exists: the Run entry is written once, when the user flips the toggle, and then nothing
// ever revisits it. A reinstall to a different root, a moved install folder, or an install performed under
// a redirected profile (a sandboxed or virtualised %LOCALAPPDATA%) all leave the entry pointing at an
// executable that no longer exists. Windows then silently does nothing at login, while the toggle still
// reads as on because an entry IS present — the user sees "start at login" enabled and an app that never
// starts. Re-asserting the expected command on startup heals that drift automatically: the registration is
// only rewritten when it is already enabled, so this never opts a user in, and Enable is idempotent so a
// correct entry is left untouched.
//
// Singleton dependencies only, so the Generic Host can own it directly.

using Application.Ports;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Logic.AppManagement.Lifecycle;

public sealed class StartupRegistrationHealingService(
	IStartupRegistration registration,
	ILogger<StartupRegistrationHealingService> logger) : IHostedService
{
	public Task StartAsync(CancellationToken cancellationToken)
	{
		if (!registration.IsEnabled())
		{
			logger.LogInformation(
				"Launch at login is not registered; the login command for this install would be {ExpectedCommand}.",
				registration.ExpectedCommand);
			return Task.CompletedTask;
		}

		string? registered = registration.RegisteredCommand;
		bool matchesThisInstall = string.Equals(registered, registration.ExpectedCommand, StringComparison.OrdinalIgnoreCase);
		if (matchesThisInstall && registration.RegisteredTargetExists)
		{
			logger.LogInformation("Launch at login is registered and current: {RegisteredCommand}.", registered);
			return Task.CompletedTask;
		}

		// Stale: the entry names another install, or an executable that is gone. Repoint it at this one.
		logger.LogWarning(
			"Launch at login was registered as {RegisteredCommand} (target exists: {TargetExists}), which does not match this "
			+ "install; repointing it at {ExpectedCommand}.",
			registered,
			registration.RegisteredTargetExists,
			registration.ExpectedCommand);
		registration.Enable();

		return Task.CompletedTask;
	}

	public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
