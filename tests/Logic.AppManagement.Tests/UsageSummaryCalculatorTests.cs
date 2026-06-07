// Unit tests for the usage-summary aggregation (WHISPER-24): empty history, summed totals (transcription
// count, characters, audio duration), and the per-day breakdown (grouping + most-recent-first ordering).
// This is the real Logic the @WHISPER-24 scenarios drive down into.

using AwesomeAssertions;
using Domain.History;
using Domain.Statistics;
using Logic.AppManagement;
using Xunit;

namespace Logic.AppManagement.Tests;

public sealed class UsageSummaryCalculatorTests
{
	private static readonly DateOnly Day1 = new(2026, 1, 1);
	private static readonly DateOnly Day2 = new(2026, 1, 2);

	private readonly UsageStatsCalculator _calculator = new();

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
}
