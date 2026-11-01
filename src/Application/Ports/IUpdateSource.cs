// Port for the release channel the app self-updates from. Implemented in Infrastructure over
// Velopack's UpdateManager against the configured GitHub Releases feed; faked in specs so the update
// policy (check -> download -> apply, and graceful degradation when the channel is unreachable) can be
// driven without any network. This is the single outbound seam for updates — the only egress, and only
// when the user clicks "Check for updates" or has opted in to the automatic startup check.

using Application.Updates;

namespace Application.Ports;

public interface IUpdateSource
{
	/// <summary>
	/// Whether this build is a Velopack install that can update itself. A local check with no network
	/// access: a dev or portable build has no installed package to update.
	/// </summary>
	bool IsInstalled { get; }

	/// <summary>
	/// Checks the channel for a newer release, returning it or <c>null</c> when the app is up to date.
	/// </summary>
	ValueTask<AvailableUpdate?> CheckForUpdatesAsync(CancellationToken cancellationToken);

	/// <summary>Downloads the given update and stages it to be applied (on next restart).</summary>
	ValueTask ApplyUpdateAsync(AvailableUpdate update, CancellationToken cancellationToken);

	/// <summary>Downloads the given update, reporting percent complete (0-100) to <paramref name="progress"/>.</summary>
	ValueTask DownloadAsync(AvailableUpdate update, IProgress<int>? progress, CancellationToken cancellationToken);

	/// <summary>
	/// Stages a downloaded update to be applied as soon as this process exits, then relaunches the app.
	/// The caller is responsible for shutting the app down afterwards.
	/// </summary>
	void ApplyOnExitAndRestart(AvailableUpdate update);
}
