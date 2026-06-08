// Port for persisting and querying transcription history. Implemented in Infrastructure (Module 9);
// faked in specs so the history/usage-stats handlers can be driven without real persistence.

using Domain.History;

namespace Application.Ports;

/// <summary>
/// Stores completed transcriptions and reads them back, newest first.
/// </summary>
/// <remarks>I/O-bound (persistence). All methods are async and honor cancellation.</remarks>
public interface IHistoryStore
{
	/// <summary>Appends a completed transcription to history.</summary>
	ValueTask AddAsync(TranscriptEntry entry, CancellationToken cancellationToken);

	/// <summary>
	/// Returns stored entries newest-first, optionally bounded by an inclusive date range
	/// (<paramref name="from"/>/<paramref name="to"/>) and/or a maximum <paramref name="limit"/>.
	/// A <c>null</c> bound means "unbounded" on that side.
	/// </summary>
	ValueTask<IReadOnlyList<TranscriptEntry>> GetEntriesAsync(
		DateTimeOffset? from,
		DateTimeOffset? to,
		int? limit,
		CancellationToken cancellationToken);

	/// <summary>
	/// Enforces the retention limit by deleting all but the most recent <paramref name="maxEntries"/>
	/// entries (by recorded time), returning the number pruned. Newer entries are never removed in
	/// preference to older ones. A non-positive <paramref name="maxEntries"/> is a no-op (unbounded).
	/// </summary>
	ValueTask<int> PruneToMostRecentAsync(int maxEntries, CancellationToken cancellationToken);

	/// <summary>Removes every transcript entry from disk (used by the user-initiated purge, WHISPER-34).</summary>
	ValueTask ClearAsync(CancellationToken cancellationToken);
}
