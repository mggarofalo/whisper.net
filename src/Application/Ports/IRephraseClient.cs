// Port for the optional AI rephrase feature. Implemented in Infrastructure by a localhost HTTP client
// (e.g. Ollama). Faked in specs. Privacy: this is the ONLY transcript-bearing network seam; it is
// opt-in, local-only, disabled by default, and never invoked unless the user enables it.

namespace Application.Ports;

/// <summary>
/// Rephrases recognized text using a locally-hosted language model, per an opt-in user instruction.
/// </summary>
/// <remarks>
/// Network-bound (local HTTP). Disabled by default and gated behind explicit user consent — see the
/// privacy stance in <c>AGENTS.md</c>. Honor cancellation so a slow model cannot stall delivery.
/// </remarks>
public interface IRephraseClient
{
	/// <summary>Returns <paramref name="text"/> rephrased according to <paramref name="instruction"/>.</summary>
	ValueTask<string> RephraseAsync(string text, string instruction, CancellationToken cancellationToken);
}
