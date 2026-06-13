// Runs the auto-update check once at startup, as a hosted service so the Generic Host owns its lifetime
//. The check runs in the background (fire-and-forget) so it never delays launch, and the
// policy it calls already swallows and logs failures — so a slow or unreachable channel can neither block
// startup nor crash the app. When auto-update is disabled (the default) the policy returns immediately
// without any network access.

using Logic.AppManagement.Updates;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Logic.AppManagement.Lifecycle;

public sealed class AutoUpdateHostedService(
	AutoUpdateService updater,
	ILogger<AutoUpdateHostedService> logger) : IHostedService
{
	public Task StartAsync(CancellationToken cancellationToken)
	{
		// Fire-and-forget: the update check must not block the host from starting the tray app. The policy
		// handles its own errors, so the only thing left to guard is an unexpected scheduling failure.
		_ = Task.Run(
			async () =>
			{
				try
				{
					await updater.UpdateIfAvailableAsync(CancellationToken.None);
				}
				catch (Exception ex)
				{
					logger.LogError(ex, "The background update check faulted unexpectedly.");
				}
			},
			CancellationToken.None);

		return Task.CompletedTask;
	}

	public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
