// Covers the UsageStats value object: the non-negative invariant on both totals, the derived
// time-saved estimate, structural equality, and the zeroed Empty instance the aggregation query
// returns for empty history.

using AwesomeAssertions;
using Domain;
using Domain.Statistics;
using Xunit;

namespace Domain.Tests;

public sealed class UsageStatsTests
{
	[Fact]
	public void Non_negative_totals_construct_successfully()
	{
		UsageStats stats = new(totalWords: 150, totalSessions: 3);

		stats.TotalWords.Should().Be(150);
		stats.TotalSessions.Should().Be(3);
	}

	[Theory]
	[InlineData(-1, 5)]
	[InlineData(100, -2)]
	[InlineData(-1, -1)]
	public void Negative_totals_are_rejected(int words, int sessions)
	{
		Action creating = () => _ = new UsageStats(words, sessions);

		creating.Should().Throw<DomainException>();
	}

	[Fact]
	public void Empty_is_fully_zeroed()
	{
		UsageStats.Empty.TotalWords.Should().Be(0);
		UsageStats.Empty.TotalSessions.Should().Be(0);
		UsageStats.Empty.EstimatedTimeSaved.Should().Be(TimeSpan.Zero);
	}

	[Fact]
	public void Estimated_time_saved_grows_with_words()
	{
		// 40 words at the assumed 40 WPM is one minute of typing saved.
		UsageStats stats = new(totalWords: 40, totalSessions: 1);

		stats.EstimatedTimeSaved.Should().Be(TimeSpan.FromMinutes(1));
	}

	[Fact]
	public void Values_with_the_same_totals_are_equal()
	{
		new UsageStats(10, 2).Should().Be(new UsageStats(10, 2));
	}
}
