// Handles InstallUpdateCommand: downloads the update, stages it to apply when this process exits, then
// asks the app to shut down gracefully so Velopack can swap in the new version and relaunch it. Shutting
// down through the app's own lifetime (the tray's Quit path) rather than letting Velopack kill the
// process means hosted services stop, settings are saved, and the instance lock is released first. A
// failed download is returned as a readable result and nothing is staged, so the app keeps running on the
// current version. Cancellation propagates.

using Application.Interfaces;
using Application.Ports;
using Microsoft.Extensions.Logging;

namespace Application.Updates;

public sealed class InstallUpdateHandler(
	IUpdateSource source,
	IAppLifetime lifetime,
	ILogger<InstallUpdateHandler> logger) : ICommandHandler<InstallUpdateCommand, UpdateInstallResult>
{
	public async ValueTask<UpdateInstallResult> Handle(InstallUpdateCommand command, CancellationToken cancellationToken)
	{
		AvailableUpdate update = new(command.Version);
		try
		{
			await source.DownloadAsync(update, command.Progress, cancellationToken);
			source.ApplyOnExitAndRestart(update);
		}
		catch (OperationCanceledException)
		{
			throw;
		}
		catch (Exception ex)
		{
			logger.LogError(ex, "Installing update {Version} failed; continuing on the current version.", command.Version);
			return new UpdateInstallResult(Restarting: false, ex.Message);
		}

		logger.LogInformation("Update {Version} staged; shutting down so it can be applied.", command.Version);
		lifetime.RequestShutdown();
		return new UpdateInstallResult(Restarting: true);
	}
}
