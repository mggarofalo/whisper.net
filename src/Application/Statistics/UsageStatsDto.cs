// Boundary projection of the UsageStats domain value for the dashboard. Mirrors the domain shape,
// including the derived time-saved estimate, so the Mapperly projection stays trivial.

namespace Application.Statistics;

public sealed record UsageStatsDto(
	int TotalWords,
	int TotalSessions,
	TimeSpan EstimatedTimeSaved);
