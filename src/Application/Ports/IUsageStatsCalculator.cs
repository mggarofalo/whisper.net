// Abstraction for aggregating transcript history into usage statistics. The behavior lives in
// Logic.AppManagement; this lets the usage-stats handler depend on the capability rather than the
// implementation (and keeps the aggregation math out of the handler, per the coding standards).

using Domain.History;
using Domain.Statistics;

namespace Application.Ports;

public interface IUsageStatsCalculator
{
	/// <summary>Aggregates the given history entries into a <see cref="UsageStats"/> value.</summary>
	UsageStats Aggregate(IReadOnlyList<TranscriptEntry> entries);
}
