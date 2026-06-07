// Removes spoken disfluencies (um, uh, er, ...) from transcribed text before it is delivered. This is
// the minimal, always-on cleanup the delivery pipeline needs today; the richer, configurable
// filler/phrase removal (e.g. "you know", "basically") and the enable/disable toggle are a Module 8
// concern.

using Application.Ports;

namespace Logic.AudioManagement;

public sealed class FillerWordCleaner : IFillerWordCleaner
{
	private static readonly HashSet<string> Fillers =
		new(StringComparer.OrdinalIgnoreCase) { "um", "uh", "er", "ah", "hmm", "mm" };

	private static readonly char[] TrimmedPunctuation = ['.', ',', '!', '?', ';', ':'];

	public string Clean(string text)
	{
		if (string.IsNullOrWhiteSpace(text))
		{
			return string.Empty;
		}

		IEnumerable<string> kept = text
			.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
			.Where(word => !Fillers.Contains(word.Trim(TrimmedPunctuation)));

		return string.Join(' ', kept);
	}
}
