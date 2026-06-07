// Unit tests for the usage-stats aggregation (WHISPER-48): empty, single-entry, and multi-entry
// history, plus the derived time-saved estimate. This is the real Logic the @WHISPER-48 scenarios
// drive down into.

using AwesomeAssertions;
using Domain.History;
using Domain.Statistics;
using Logic.AppManagement;
using Xunit;

namespace Logic.AppManagement.Tests;

public sealed class UsageStatsCalculatorTests
{
	private static readonly DateTimeOffset When = new(2026, 1, 1, 10, 0, 0, TimeSpan.Zero);
	private readonly UsageStatsCalculator _calculator = new();

	// Builds an entry whose word count is exactly `words`.
	private static TranscriptEntry EntryWith(int words) =>
		new(Guid.NewGuid(), string.Join(' ', Enumerable.Repeat("word", words)), When);

	[Fact]
	public void Empty_history_yields_zeroed_stats()
	{
		UsageStats stats = _calculator.Aggregate([]);

		stats.Should().Be(UsageStats.Empty);
		stats.TotalWords.Should().Be(0);
		stats.TotalSessions.Should().Be(0);
		stats.EstimatedTimeSaved.Should().Be(TimeSpan.Zero);
	}

	[Fact]
	public void A_single_entry_reports_its_words_and_one_session()
	{
		UsageStats stats = _calculator.Aggregate([EntryWith(12)]);

		stats.TotalWords.Should().Be(12);
		stats.TotalSessions.Should().Be(1);
	}

	[Fact]
	public void Multiple_entries_sum_words_and_count_sessions()
	{
		UsageStats stats = _calculator.Aggregate([EntryWith(50), EntryWith(50), EntryWith(50)]);

		stats.TotalWords.Should().Be(150);
		stats.TotalSessions.Should().Be(3);
	}

	[Fact]
	public void Estimated_time_saved_reflects_the_total_words()
	{
		// 80 words at the assumed 40 WPM is two minutes of typing saved.
		UsageStats stats = _calculator.Aggregate([EntryWith(40), EntryWith(40)]);

		stats.EstimatedTimeSaved.Should().Be(TimeSpan.FromMinutes(2));
	}
}
