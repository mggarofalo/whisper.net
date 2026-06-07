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

	public UsageSummary Summarize(IReadOnlyList<TranscriptEntry> entries)
	{
		if (entries.Count == 0)
		{
			return UsageSummary.Empty;
		}

		int totalCharacters = 0;
		TimeSpan totalAudio = TimeSpan.Zero;
		foreach (TranscriptEntry entry in entries)
		{
			totalCharacters += entry.Text.Length;
			totalAudio += entry.AudioDuration;
		}

		// Group by the calendar day the transcription happened on (in its own offset), most-recent day first.
		List<DailyUsage> byDay = entries
			.GroupBy(entry => DateOnly.FromDateTime(entry.CreatedAt.Date))
			.Select(group => new DailyUsage(
				group.Key,
				group.Count(),
				group.Sum(entry => entry.Text.Length),
				group.Aggregate(TimeSpan.Zero, (running, entry) => running + entry.AudioDuration)))
			.OrderByDescending(day => day.Day)
			.ToList();

		return new UsageSummary(entries.Count, totalCharacters, totalAudio, byDay);
	}
}
