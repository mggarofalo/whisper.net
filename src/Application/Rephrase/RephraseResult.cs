// The outcome of an AI-rephrase attempt (WHISPER-40). A plain DTO with no HTTP/network types, so the
// Application port stays infrastructure-agnostic. Carries the resulting Text alongside a Status the
// caller can branch on: Rephrased (the model rewrote it), Disabled (opt-in off — Text is the original,
// untouched, and no network call was made), or Failed (the backend was unreachable/errored — Text is
// the original, so the pipeline degrades gracefully rather than losing the user's words).

namespace Application.Rephrase;

public enum RephraseStatus
{
	Rephrased,
	Disabled,
	Failed,
}

public sealed record RephraseResult(RephraseStatus Status, string Text)
{
	public static RephraseResult Rephrased(string text) => new(RephraseStatus.Rephrased, text);

	public static RephraseResult Disabled(string text) => new(RephraseStatus.Disabled, text);

	public static RephraseResult Failed(string text) => new(RephraseStatus.Failed, text);
}
