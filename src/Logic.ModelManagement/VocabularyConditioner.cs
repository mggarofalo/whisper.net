// Assembles decoder conditioning from a user-supplied custom vocabulary (WHISPER-38), mirroring
// speaktype's prompt-token approach. Pure and stateless: it reads only its argument, so an edited
// vocabulary is reflected on the very next call — the caller can re-assemble per transcription without
// restarting the engine. No native model is involved, so the assembly is fully unit-testable in
// isolation.

using Domain.Models;

namespace Logic.ModelManagement;

public sealed class VocabularyConditioner
{
	/// <summary>
	/// Turns <paramref name="vocabulary"/> into <see cref="DecodingOptions"/>. A non-empty vocabulary
	/// becomes the decoder's initial prompt and disables the first-token log-probability threshold; an
	/// empty (or all-blank) vocabulary yields <see cref="DecodingOptions.Default"/> — decoding unchanged.
	/// </summary>
	public DecodingOptions Assemble(IReadOnlyList<string>? vocabulary)
	{
		// Keep only meaningful terms; blank/whitespace entries condition nothing.
		string[] terms = vocabulary is null
			? []
			: [.. vocabulary.Where(term => !string.IsNullOrWhiteSpace(term)).Select(term => term.Trim())];

		if (terms.Length == 0)
		{
			return DecodingOptions.Default;
		}

		// The terms become the decoder's initial prompt, biasing recognition toward them. Disable the
		// first-token log-probability threshold whenever a prompt is supplied: the injected prompt can push
		// the genuine first sampled token below that threshold and drop it (speaktype's documented gotcha).
		return new DecodingOptions(
			InitialPrompt: string.Join(", ", terms),
			DisableFirstTokenLogProbThreshold: true);
	}
}
