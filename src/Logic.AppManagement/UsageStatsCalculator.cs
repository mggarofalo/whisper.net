// Aggregates transcript history into usage statistics: total words dictated (summed from each entry's
// word count) and total sessions (one per entry). The derived time-saved estimate is computed inside
// UsageStats. Deterministic and total — empty history yields zeroed stats and never throws.

using Application.Ports;
using Domain.History;
using Domain.Statistics;

namespace Logic.AppManagement;

public sealed class UsageStatsCalculator : IUsageStatsCalculator
{
	public UsageStats Aggregate(IReadOnlyList<TranscriptEntry> entries)
	{
		if (entries.Count == 0)
		{
			return UsageStats.Empty;
		}

		int totalWords = 0;
		foreach (TranscriptEntry entry in entries)
		{
			totalWords += entry.WordCount;
		}

		return new UsageStats(totalWords, entries.Count);
	}
}
