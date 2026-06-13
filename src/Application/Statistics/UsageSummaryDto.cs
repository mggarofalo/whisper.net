// Boundary projection of the UsageSummary domain aggregate for the dashboard. Mirrors the
// domain shape — overall totals plus the per-day breakdown — so the Mapperly projection stays trivial.

namespace Application.Statistics;

public sealed record DailyUsageDto(
	DateOnly Day,
	int Transcriptions,
	int Characters,
	TimeSpan AudioDuration);

public sealed record UsageSummaryDto(
	int TotalTranscriptions,
	int TotalCharacters,
	TimeSpan TotalAudioDuration,
	IReadOnlyList<DailyUsageDto> ByDay);
