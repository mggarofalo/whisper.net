// Pins the dashboard's time-saved display (WHISPER-142): a benefit reading like "4h 23m" rather than the
// "04:23:37" stopwatch a TimeSpan StringFormat produced.

using AwesomeAssertions;
using Logic.AppManagement.Shell;
using Xunit;

namespace Logic.AppManagement.Tests.Shell;

public sealed class TimeSavedFormatterTests
{
	[Theory]
	[InlineData(4, 23, 37, "4h 23m")]
	[InlineData(1, 0, 0, "1h 0m")]
	[InlineData(30, 5, 0, "30h 5m")]
	[InlineData(0, 23, 59, "23m")]
	[InlineData(0, 0, 45, "45s")]
	[InlineData(0, 0, 0, "0s")]
	public void Formats_hours_and_minutes_dropping_seconds_once_past_a_minute(int hours, int minutes, int seconds, string expected) =>
		TimeSavedFormatter.Format(new TimeSpan(hours, minutes, seconds)).Should().Be(expected);

	[Fact]
	public void A_negative_estimate_reads_as_zero() =>
		TimeSavedFormatter.Format(TimeSpan.FromMinutes(-3)).Should().Be("0s");
}
