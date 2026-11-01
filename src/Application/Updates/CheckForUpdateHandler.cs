// Handles CheckForUpdateQuery: asks the release channel whether a newer version exists. A build that is
// not a Velopack install is answered locally (NotInstalled) without touching the network. Any failure is
// returned as a readable Failed result rather than thrown, so a broken connection never takes the
// settings page — or the app — down. Cancellation propagates.

using Application.Interfaces;
using Application.Ports;
using Microsoft.Extensions.Logging;

namespace Application.Updates;

public sealed class CheckForUpdateHandler(IUpdateSource source, ILogger<CheckForUpdateHandler> logger)
	: IQueryHandler<CheckForUpdateQuery, UpdateCheckResult>
{
	public async ValueTask<UpdateCheckResult> Handle(CheckForUpdateQuery query, CancellationToken cancellationToken)
	{
		if (!source.IsInstalled)
		{
			return new UpdateCheckResult(UpdateCheckStatus.NotInstalled);
		}

		try
		{
			AvailableUpdate? update = await source.CheckForUpdatesAsync(cancellationToken);
			return update is null
				? new UpdateCheckResult(UpdateCheckStatus.UpToDate)
				: new UpdateCheckResult(UpdateCheckStatus.Available, update.Version);
		}
		catch (OperationCanceledException)
		{
			throw;
		}
		catch (Exception ex)
		{
			logger.LogError(ex, "The update check failed.");
			return new UpdateCheckResult(UpdateCheckStatus.Failed, Error: ex.Message);
		}
	}
}
