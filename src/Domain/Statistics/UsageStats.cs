// Aggregate usage the dashboard surfaces: how many words were dictated, across how many sessions,
// and an estimate of the typing time that saved. A value object — equal by its values, never
// negative. EstimatedTimeSaved is derived from the word count at an assumed typing speed, so it is
// always consistent with TotalWords (which keeps the value round-trip-safe through its DTO).

namespace Domain.Statistics;

public sealed record UsageStats
{
	// Assumed typing speed used to estimate the time dictation saved versus typing the same words.
	private const double AssumedWordsPerMinute = 40.0;

	public int TotalWords { get; }
	public int TotalSessions { get; }
	public TimeSpan EstimatedTimeSaved { get; }

	public UsageStats(int totalWords, int totalSessions)
	{
		if (totalWords < 0)
		{
			throw new DomainException("Total words must not be negative.");
		}

		if (totalSessions < 0)
		{
			throw new DomainException("Total sessions must not be negative.");
		}

		TotalWords = totalWords;
		TotalSessions = totalSessions;
		EstimatedTimeSaved = TimeSpan.FromMinutes(totalWords / AssumedWordsPerMinute);
	}

	// The stats for a user who has dictated nothing yet.
	public static UsageStats Empty { get; } = new(0, 0);
}
