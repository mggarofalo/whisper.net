// Thin step definitions for the local-day bucketing feature. Each step delegates to the
// UsageStatsLocalDayDriver (injected by the Reqnroll DI plugin); no logic lives here.

using Dictation.Specs.Drivers;
using Reqnroll;

namespace Dictation.Specs.StepDefinitions;

[Binding]
public sealed class UsageStatsLocalDaySteps(UsageStatsLocalDayDriver driver)
{
	[Given(@"the user's time zone is (\d+) hours behind UTC")]
	public void GivenTimeZoneHoursBehind(int hours) => driver.TimeZoneIsHoursBehindUtc(hours);

	[Given(@"a dictation recorded at (.*) UTC")]
	public void GivenDictationRecordedAt(string utcTimestamp) => driver.DictationRecordedAtUtc(utcTimestamp);

	[When(@"the usage summary is calculated")]
	public void WhenSummaryCalculated() => driver.CalculateSummary();

	[Then(@"the summary has (\d+) daily buckets")]
	public void ThenDailyBuckets(int expected) => driver.AssertDailyBuckets(expected);

	[Then(@"the all-time transcription total is (\d+)")]
	public void ThenAllTimeTotal(int expected) => driver.AssertAllTimeTotal(expected);
}
