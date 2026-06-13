// Implements IUpdateSource over Velopack's UpdateManager against the configured GitHub Releases feed
// — the single outbound seam for updates. The UpdateManager is created lazily on first use,
// not in the constructor: building it resolves the app's install locator, which throws on a dev/F5 build
// (no local package), so constructing this adapter must touch none of that — resolving the port stays
// safe in any host. Only an installed (Velopack-packaged) app can self-update, so off an install the
// check is a no-op that reports "up to date", keeping the policy layer simple. Downloading stages the
// update to apply on the next exit rather than force-restarting, so a running dictation session is never
// interrupted. Network/Velopack errors surface as exceptions; the AutoUpdateService policy catches them.

using Application.Configuration;
using Application.Ports;
using Application.Updates;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Velopack;
using GithubSource = Velopack.Sources.GithubSource;

namespace Infrastructure.Updates;

public sealed class VelopackUpdateSource(IOptions<AutoUpdateOptions> options, ILogger<VelopackUpdateSource> logger)
	: IUpdateSource
{
	private readonly AutoUpdateOptions _config = options.Value;

	public async ValueTask<AvailableUpdate?> CheckForUpdatesAsync(CancellationToken cancellationToken)
	{
		UpdateManager? manager = TryCreateManager();
		if (manager is null || !manager.IsInstalled)
		{
			// A non-installed build (or no usable locator) has no local package to update; treat as current.
			logger.LogDebug("Not a Velopack installation; skipping the update check.");
			return null;
		}

		UpdateInfo? info = await manager.CheckForUpdatesAsync().ConfigureAwait(false);
		return info is null ? null : new AvailableUpdate(info.TargetFullRelease.Version.ToString());
	}

	public async ValueTask ApplyUpdateAsync(AvailableUpdate update, CancellationToken cancellationToken)
	{
		UpdateManager? manager = TryCreateManager();
		if (manager is null || !manager.IsInstalled)
		{
			return;
		}

		UpdateInfo? info = await manager.CheckForUpdatesAsync().ConfigureAwait(false);
		if (info is null)
		{
			return;
		}

		await manager.DownloadUpdatesAsync(info).ConfigureAwait(false);

		// Apply on exit, not now: the user keeps the running version until they next restart the app.
		manager.WaitExitThenApplyUpdates(info);
	}

	// Building the UpdateManager resolves the install locator, which throws when the app is not a Velopack
	// install. Treat that as "no updater available" rather than letting it propagate from a port resolution.
	private UpdateManager? TryCreateManager()
	{
		try
		{
			return new UpdateManager(new GithubSource(_config.RepositoryUrl, accessToken: null, prerelease: _config.IncludePreReleases));
		}
		catch (Exception ex)
		{
			logger.LogDebug(ex, "The Velopack update manager is unavailable (not a packaged install); skipping update.");
			return null;
		}
	}
}
