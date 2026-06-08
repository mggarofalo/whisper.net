// Port for the release channel the app self-updates from (WHISPER-29). Implemented in Infrastructure over
// Velopack's UpdateManager against the configured GitHub Releases feed; faked in specs so the update
// policy (check -> download -> apply, and graceful degradation when the channel is unreachable) can be
// driven without any network. This is the single outbound seam for updates — the only egress, and only
// when auto-update is opted in.

using Application.Updates;

namespace Application.Ports;

public interface IUpdateSource
{
	/// <summary>
	/// Checks the channel for a newer release, returning it or <c>null</c> when the app is up to date.
	/// </summary>
	ValueTask<AvailableUpdate?> CheckForUpdatesAsync(CancellationToken cancellationToken);

	/// <summary>Downloads the given update and stages it to be applied (on next restart).</summary>
	ValueTask ApplyUpdateAsync(AvailableUpdate update, CancellationToken cancellationToken);
}
