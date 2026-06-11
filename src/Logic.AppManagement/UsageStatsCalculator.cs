// Aggregates transcript history into usage statistics: total words dictated (summed from each entry's
// word count) and total sessions (one per entry). The derived time-saved estimate is computed inside
// UsageStats. Deterministic and total — empty history yields zeroed stats and never throws. The per-day
// breakdown (Summarize) buckets by the user's LOCAL calendar day via the injected TimeProvider's
// LocalTimeZone (WHISPER-116) — not the UTC day — so a dictation just before local midnight lands on the
// correct day; the zone is injected (not the ambient ToLocalTime) so it stays deterministic in tests.

using Application.Ports;
using Domain.History;
using Domain.Statistics;

namespace Logic.AppManagement;

public sealed class UsageStatsCalculator(TimeProvider timeProvider) : IUsageStatsCalculator
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

		// Group by the user's LOCAL calendar day (WHISPER-116), most-recent day first. The entry's offset is
		// UTC, so converting through the injected LocalTimeZone is what puts a late-evening dictation on the
		// right local day instead of tomorrow's UTC day.
		TimeZoneInfo zone = timeProvider.LocalTimeZone;
		List<DailyUsage> byDay = entries
			.GroupBy(entry => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(entry.CreatedAt, zone).Date))
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
