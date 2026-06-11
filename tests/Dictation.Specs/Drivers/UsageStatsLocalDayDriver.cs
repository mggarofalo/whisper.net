// Drives the @WHISPER-116 local-day bucketing scenarios over the REAL UsageStatsCalculator. It pins a
// non-UTC time zone (via a ManualTimeProvider, the same TimeProvider seam production injects) so the
// assertion is deterministic regardless of the host's zone: two dictations whose UTC day is identical but
// whose LOCAL day differs must land in different daily buckets. All-time totals stay zone-independent.

using System.Globalization;
using AwesomeAssertions;
using Dictation.Specs.Support;
using Domain.History;
using Domain.Statistics;
using Logic.AppManagement;

namespace Dictation.Specs.Drivers;

public sealed class UsageStatsLocalDayDriver
{
	private readonly List<TranscriptEntry> _entries = [];
	private TimeZoneInfo _zone = TimeZoneInfo.Utc;
	private UsageSummary? _summary;

	public void TimeZoneIsHoursBehindUtc(int hours) =>
		_zone = TimeZoneInfo.CreateCustomTimeZone($"Test-{hours}", TimeSpan.FromHours(-hours), "Test", "Test");

	public void DictationRecordedAtUtc(string utcTimestamp)
	{
		DateTimeOffset when = DateTimeOffset.Parse(
			utcTimestamp,
			CultureInfo.InvariantCulture,
			DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal);
		_entries.Add(new TranscriptEntry(Guid.NewGuid(), "dictation", when));
	}

	public void CalculateSummary()
	{
		ManualTimeProvider time = new();
		time.SetLocalTimeZone(_zone);
		_summary = new UsageStatsCalculator(time).Summarize(_entries);
	}

	public void AssertDailyBuckets(int expected) =>
		_summary!.ByDay.Should().HaveCount(expected, "daily buckets reflect the user's local calendar day");

	public void AssertAllTimeTotal(int expected) =>
		_summary!.TotalTranscriptions.Should().Be(expected, "the all-time total is unaffected by the day bucketing");
}
