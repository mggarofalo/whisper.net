// Drives the domain-invariant scenarios. Unlike the transcription driver, these behaviors live
// entirely in the Domain layer, so the driver constructs domain types directly (no IMediator) and
// captures whatever the construction/transition throws. The steps then assert on whether the domain
// rejected the operation. Scenario-scoped, so its captured state is fresh per scenario.

using AwesomeAssertions;
using Domain;
using Domain.History;
using Domain.Recording;
using Domain.Statistics;

namespace Dictation.Specs.Drivers;

public sealed class DomainInvariantsDriver
{
	private RecordingSession? _session;
	private Exception? _caught;

	public void StartSessionAt(string timeOfDay) =>
		_session = RecordingSession.Start(AtTime(timeOfDay));

	public void EndSessionAt(string timeOfDay) =>
		_caught = Capture(() => _session!.End(AtTime(timeOfDay)));

	public void CreateTranscriptWithEmptyText() =>
		_caught = Capture(() => TranscriptEntry.Create(string.Empty, AtTime("10:00:00")));

	public void CreateUsageStats(int words, int sessions) =>
		_caught = Capture(() => _ = new UsageStats(words, sessions));

	public void AssertRejectedAsInvariantViolation() =>
		_caught.Should().BeOfType<DomainException>();

	public void AssertConstructionSucceeded() =>
		_caught.Should().BeNull();

	// Runs an action and returns the exception it threw, or null if it completed.
	private static Exception? Capture(Action action)
	{
		try
		{
			action();
			return null;
		}
		catch (Exception ex)
		{
			return ex;
		}
	}

	// Anchors a wall-clock time to a fixed date so start/end comparisons are deterministic.
	private static DateTimeOffset AtTime(string timeOfDay)
	{
		TimeOnly time = TimeOnly.ParseExact(timeOfDay, "HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture);
		return new DateTimeOffset(2026, 1, 1, time.Hour, time.Minute, time.Second, TimeSpan.Zero);
	}
}
