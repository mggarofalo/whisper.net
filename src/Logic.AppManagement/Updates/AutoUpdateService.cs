// The in-app auto-update policy (WHISPER-29): the single place that decides whether to check, download,
// and apply an update, and — crucially — that a failed or unreachable update never takes the app down.
// It honors the opt-in switch first (no check, so no network egress, when disabled), then checks the
// release source; if a newer release is found it downloads and stages it to apply on the next restart.
// Any failure (channel unreachable, download error) is logged via Serilog and swallowed: the app keeps
// running on the current version. Cancellation propagates. Pure policy over the IUpdateSource port, so it
// is unit- and BDD-tested with a faked source — no Velopack, no network.

using Application.Configuration;
using Application.Ports;
using Application.Updates;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Logic.AppManagement.Updates;

public sealed class AutoUpdateService(
	IUpdateSource source,
	IOptions<AutoUpdateOptions> options,
	ILogger<AutoUpdateService> logger)
{
	public async ValueTask<UpdateOutcome> UpdateIfAvailableAsync(CancellationToken cancellationToken)
	{
		if (!options.Value.Enabled)
		{
			// Opt-in is off: do not touch the network at all.
			logger.LogDebug("Auto-update is disabled; skipping the update check.");
			return UpdateOutcome.Disabled;
		}

		try
		{
			AvailableUpdate? update = await source.CheckForUpdatesAsync(cancellationToken);
			if (update is null)
			{
				logger.LogInformation("No update available; running the current version.");
				return UpdateOutcome.UpToDate;
			}

			await source.ApplyUpdateAsync(update, cancellationToken);
			logger.LogInformation("Update {Version} downloaded; it will be applied on the next restart.", update.Version);
			return UpdateOutcome.Updated;
		}
		catch (OperationCanceledException)
		{
			throw;
		}
		catch (Exception ex)
		{
			// A failed or unreachable update must never crash the app — keep running on the current version.
			logger.LogError(ex, "Update check failed; continuing on the current version.");
			return UpdateOutcome.Failed;
		}
	}
}
