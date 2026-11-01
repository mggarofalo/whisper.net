// Implements IUpdateSource over Velopack's UpdateManager against the configured GitHub Releases feed
// — the single outbound seam for updates. The UpdateManager is created lazily on first use,
// not in the constructor: building it resolves the app's install locator, which throws on a dev/F5 build
// (no local package), so constructing this adapter must touch none of that — resolving the port stays
// safe in any host. Only an installed (Velopack-packaged) app can self-update, so off an install the
// check is a no-op that reports "up to date", keeping the policy layer simple; IsInstalled lets the
// user-initiated check say so instead. The automatic path stages a download to apply on the next exit
// rather than force-restarting, so a running dictation session is never interrupted; the user-initiated
// path stages it with a relaunch, and the caller then shuts the app down gracefully. Network/Velopack
// errors surface as exceptions; the AutoUpdateService policy and the update handlers catch them.

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
	private readonly Lock _gate = new();
	private UpdateManager? _manager;
	private bool _managerResolved;

	// The release the last check found, so a download does not have to ask the feed again.
	private UpdateInfo? _lastFound;

	// The release the user-initiated download fetched, which ApplyOnExitAndRestart stages.
	private VelopackAsset? _downloaded;

	public bool IsInstalled => GetManager() is { IsInstalled: true };

	public async ValueTask<AvailableUpdate?> CheckForUpdatesAsync(CancellationToken cancellationToken)
	{
		UpdateManager? manager = GetManager();
		if (manager is null || !manager.IsInstalled)
		{
			// A non-installed build (or no usable locator) has no local package to update; treat as current.
			logger.LogDebug("Not a Velopack installation; skipping the update check.");
			return null;
		}

		UpdateInfo? info = await manager.CheckForUpdatesAsync().ConfigureAwait(false);
		_lastFound = info;
		return info is null ? null : new AvailableUpdate(info.TargetFullRelease.Version.ToString());
	}

	public async ValueTask ApplyUpdateAsync(AvailableUpdate update, CancellationToken cancellationToken)
	{
		UpdateManager? manager = GetManager();
		if (manager is null || !manager.IsInstalled)
		{
			return;
		}

		UpdateInfo? info = await manager.CheckForUpdatesAsync().ConfigureAwait(false);
		if (info is null)
		{
			return;
		}

		await manager.DownloadUpdatesAsync(info, cancelToken: cancellationToken).ConfigureAwait(false);

		// Apply on exit, not now: the user keeps the running version until they next restart the app.
		manager.WaitExitThenApplyUpdates(info);
	}

	public async ValueTask DownloadAsync(AvailableUpdate update, IProgress<int>? progress, CancellationToken cancellationToken)
	{
		UpdateManager manager = RequireInstalledManager();
		UpdateInfo info = await FindAsync(manager, update).ConfigureAwait(false);
		await manager.DownloadUpdatesAsync(info, percent => progress?.Report(percent), cancellationToken).ConfigureAwait(false);
		_downloaded = info.TargetFullRelease;
	}

	public void ApplyOnExitAndRestart(AvailableUpdate update)
	{
		UpdateManager manager = RequireInstalledManager();
		VelopackAsset asset = (_downloaded?.Version.ToString() == update.Version ? _downloaded : manager.UpdatePendingRestart)
			?? throw new InvalidOperationException($"Update {update.Version} has not been downloaded.");

		// Velopack's updater waits for this process to exit, applies the update, then relaunches the app.
		manager.WaitExitThenApplyUpdates(asset, silent: true, restart: true);
		logger.LogInformation("Update {Version} will be applied when the app exits.", asset.Version);
	}

	// Reuse the release the last check found when it is the one the user chose; otherwise ask the feed.
	private async Task<UpdateInfo> FindAsync(UpdateManager manager, AvailableUpdate update)
	{
		UpdateInfo? info = _lastFound;
		if (info is null || info.TargetFullRelease.Version.ToString() != update.Version)
		{
			info = await manager.CheckForUpdatesAsync().ConfigureAwait(false);
		}

		if (info is null || info.TargetFullRelease.Version.ToString() != update.Version)
		{
			throw new InvalidOperationException($"Version {update.Version} is no longer offered by the release feed.");
		}

		return info;
	}

	private UpdateManager RequireInstalledManager() =>
		GetManager() is { IsInstalled: true } manager
			? manager
			: throw new InvalidOperationException("Only the installed app can update itself.");

	// Building the UpdateManager resolves the install locator, which throws when the app is not a Velopack
	// install. Treat that as "no updater available" rather than letting it propagate from a port resolution.
	// Resolved once: the install layout does not change while the app runs.
	private UpdateManager? GetManager()
	{
		lock (_gate)
		{
			if (!_managerResolved)
			{
				_managerResolved = true;
				try
				{
					_manager = new UpdateManager(new GithubSource(_config.RepositoryUrl, accessToken: null, prerelease: _config.IncludePreReleases));
				}
				catch (Exception ex)
				{
					logger.LogDebug(ex, "The Velopack update manager is unavailable (not a packaged install); skipping update.");
				}
			}

			return _manager;
		}
	}
}
