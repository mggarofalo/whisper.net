// Normalizes raw Whisper output before it is delivered (WHISPER-36). Two independent steps:
//   1. Noise-label stripping (ALWAYS on): Whisper emits bracketed/parenthesized non-speech labels such
//      as [BLANK_AUDIO], [SILENCE], [ S ], or (music). These are removed whole, brackets included.
//   2. Filler-word removal (GATED by the "remove filler words" setting): spoken disfluencies (um, uh,
//      erm, hmm, mhm, ...) — including their natural elongations (ummm, uhhh) — are removed on word
//      boundaries, eating a trailing comma/period the removed word would otherwise strand.
// Whitespace is collapsed and the result trimmed, so a filler at the start of a sentence leaves no
// stranded leading space or punctuation. The function is pure and idempotent: running it twice yields
// the same output.

using System.Text.RegularExpressions;
using Application.Ports;

namespace Logic.AudioManagement;

public sealed partial class FillerWordCleaner : IFillerWordCleaner
{
	// Bracketed or parenthesized noise labels, stripped whole including the delimiters. The content is
	// arbitrary (Whisper varies the label), so we remove anything between matched brackets/parentheses.
	[GeneratedRegex(@"\[[^\]]*\]|\([^)]*\)")]
	private static partial Regex NoiseLabelRegex();

	// Disfluencies on word boundaries, allowing the elongation natural to speech (um/ummm, uh/uhhh,
	// hmm/hmmm, mhm/mhmm). A single trailing comma or period left dangling by the removed word is eaten.
	// Case-insensitive. Longer forms (erm, mhm) precede their shorter prefixes (er, mm) in the
	// alternation so the engine prefers the fuller match.
	[GeneratedRegex(@"\b(?:u+m+|u+h+|e+r+m+|m+h+m*|h+m+|a+h+|e+r+|m+m+)\b[,.]?", RegexOptions.IgnoreCase)]
	private static partial Regex FillerRegex();

	[GeneratedRegex(@"\s+")]
	private static partial Regex WhitespaceRegex();

	public string Clean(string text, bool removeFillerWords = true)
	{
		if (string.IsNullOrWhiteSpace(text))
		{
			return string.Empty;
		}

		// Replace removed spans with a space rather than nothing, so adjacent words never fuse; the final
		// whitespace collapse + trim cleans up the spaces (and any leading one a removed first word left).
		string result = NoiseLabelRegex().Replace(text, " ");

		if (removeFillerWords)
		{
			result = FillerRegex().Replace(result, " ");
		}

		return WhitespaceRegex().Replace(result, " ").Trim();
	}
}
