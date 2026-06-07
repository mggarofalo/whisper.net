// History retention policy (WHISPER-17): how much transcription history is kept. Bound from the
// "History" configuration section; the default is a sane, finite cap so history cannot grow without
// bound on a fresh install. The policy lives in the Application layer (out of Infrastructure) and is
// enforced after each new history write by pruning the oldest entries beyond the limit.

namespace Application.Configuration;

public sealed class RetentionOptions
{
	public const string SectionName = "History";

	/// <summary>
	/// Maximum number of transcript entries to retain. After each new entry is recorded, entries beyond
	/// the most recent <see cref="MaxEntries"/> are pruned. A value of zero or less disables pruning
	/// (unbounded history). Defaults to 1000.
	/// </summary>
	public int MaxEntries { get; set; } = 1000;
}
