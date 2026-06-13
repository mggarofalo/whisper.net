// Unit tests for the usage-summary aggregation: empty history, summed totals (transcription
// count, characters, audio duration), and the per-day breakdown (grouping + most-recent-first ordering).
// This is the real Logic the acceptance scenarios drive down into.

using AwesomeAssertions;
using Domain.History;
using Domain.Statistics;
using Logic.AppManagement;
using Logic.AppManagement.Tests.Support;
using Xunit;

namespace Logic.AppManagement.Tests;

public sealed class UsageSummaryCalculatorTests
{
	private static readonly DateOnly Day1 = new(2026, 1, 1);
	private static readonly DateOnly Day2 = new(2026, 1, 2);

	// Default zone is UTC (ManualTimeProvider), so the mid-day (09:00 UTC) entries below bucket by their
	// calendar day exactly as before; the local-day behavior is pinned separately with a non-UTC zone.
	private readonly UsageStatsCalculator _calculator = new(new ManualTimeProvider());

	private static TranscriptEntry Entry(DateOnly day, int characters, int seconds)
	{
		string text = new('x', characters);
		DateTimeOffset when = new(day.Year, day.Month, day.Day, 9, 0, 0, TimeSpan.Zero);
		return new TranscriptEntry(Guid.NewGuid(), text, when, TimeSpan.FromSeconds(seconds));
	}

	[Fact]
	public void Empty_history_yields_an_empty_summary()
	{
		UsageSummary summary = _calculator.Summarize([]);

		summary.Should().Be(UsageSummary.Empty);
		summary.ByDay.Should().BeEmpty();
	}

	[Fact]
	public void Totals_sum_transcriptions_characters_and_audio_duration()
	{
		UsageSummary summary = _calculator.Summarize([Entry(Day1, characters: 80, seconds: 12), Entry(Day1, characters: 40, seconds: 8)]);

		summary.TotalTranscriptions.Should().Be(2);
		summary.TotalCharacters.Should().Be(120);
		summary.TotalAudioDuration.Should().Be(TimeSpan.FromSeconds(20));
	}

	[Fact]
	public void Breakdown_groups_by_day_most_recent_first()
	{
		UsageSummary summary = _calculator.Summarize(
		[
			Entry(Day1, characters: 30, seconds: 5),
			Entry(Day2, characters: 10, seconds: 3),
			Entry(Day1, characters: 20, seconds: 7),
		]);

		summary.ByDay.Should().HaveCount(2);

		DailyUsage first = summary.ByDay[0];
		first.Day.Should().Be(Day2);
		first.Transcriptions.Should().Be(1);
		first.Characters.Should().Be(10);
		first.AudioDuration.Should().Be(TimeSpan.FromSeconds(3));

		DailyUsage second = summary.ByDay[1];
		second.Day.Should().Be(Day1);
		second.Transcriptions.Should().Be(2);
		second.Characters.Should().Be(50);
		second.AudioDuration.Should().Be(TimeSpan.FromSeconds(12));
	}

	[Fact]
	public void Breakdown_buckets_by_the_local_day_not_the_utc_day()
	{
		// A UTC-05:00 user. Both entries share the UTC date 2026-06-12, but in local time they
		// straddle midnight — 04:30 UTC is 23:30 on the 11th, 05:30 UTC is 00:30 on the 12th — so they must
		// fall on DIFFERENT local days. The UTC-day grouping put both on the 12th.
		ManualTimeProvider time = new();
		time.SetLocalTimeZone(TimeZoneInfo.CreateCustomTimeZone("Test-05", TimeSpan.FromHours(-5), "Test-05", "Test-05"));
		UsageStatsCalculator calculator = new(time);

		TranscriptEntry late = new(Guid.NewGuid(), "late night", new DateTimeOffset(2026, 6, 12, 4, 30, 0, TimeSpan.Zero));
		TranscriptEntry justAfter = new(Guid.NewGuid(), "just after", new DateTimeOffset(2026, 6, 12, 5, 30, 0, TimeSpan.Zero));

		UsageSummary summary = calculator.Summarize([late, justAfter]);

		summary.ByDay.Should().HaveCount(2, "the entries fall on different LOCAL days though their UTC day is the same");
		summary.ByDay.Select(day => day.Day).Should().BeEquivalentTo([new DateOnly(2026, 6, 12), new DateOnly(2026, 6, 11)]);
	}
}
