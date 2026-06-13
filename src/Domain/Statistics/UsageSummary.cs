// A richer usage aggregate than UsageStats: how many transcriptions were recorded, the
// total characters produced and audio time captured, plus a per-day breakdown so the dashboard can chart
// usage over time. Computed from transcript history; the per-day rows carry the same measures scoped to a
// single calendar day. All totals are non-negative by construction (they sum non-negative entry values).

namespace Domain.Statistics;

public sealed record DailyUsage(DateOnly Day, int Transcriptions, int Characters, TimeSpan AudioDuration);

public sealed record UsageSummary(
	int TotalTranscriptions,
	int TotalCharacters,
	TimeSpan TotalAudioDuration,
	IReadOnlyList<DailyUsage> ByDay)
{
	// The summary for a user who has dictated nothing yet.
	public static UsageSummary Empty { get; } = new(0, 0, TimeSpan.Zero, []);
}
