// Decoder conditioning derived from the user's custom vocabulary (WHISPER-38). The InitialPrompt biases
// recognition toward the supplied terms via Whisper's prompt-token conditioning. When a prompt is
// present the first-token log-probability threshold must be disabled: the injected prompt can push the
// genuine first sampled token below that threshold and drop it. With no vocabulary, both are left at
// their defaults so decoding is unchanged. A value object passed from the assembler (Logic) down to the
// native engine (Infrastructure).

namespace Domain.Models;

public sealed record DecodingOptions(string? InitialPrompt, bool DisableFirstTokenLogProbThreshold)
{
	/// <summary>No conditioning: no initial prompt, and the first-token threshold keeps its default.</summary>
	public static DecodingOptions Default { get; } = new(InitialPrompt: null, DisableFirstTokenLogProbThreshold: false);
}
