// The single post-processing configuration section: every knob the post-process pipeline
// reads lives here — whether to strip filler words, the custom vocabulary that conditions the decoder
// (applied upstream during transcription), the default output transform to apply, and the opt-in AI
// rephrase enable + endpoint. Bound from the "PostProcess" section and held live so edits take effect
// on the next transcription without an app restart.

namespace Application.Configuration;

public sealed class PostProcessOptions
{
	public const string SectionName = "PostProcess";

	/// <summary>Strip spoken filler words during normalization. Noise-label stripping is always on regardless.</summary>
	public bool RemoveFillerWords { get; set; } = true;

	/// <summary>Terms/phrases that bias the decoder via prompt-token conditioning (applied upstream during transcription).</summary>
	public IReadOnlyList<string> CustomVocabulary { get; set; } = [];

	/// <summary>Name of the output transform to apply by default, or null/empty for none.</summary>
	public string? DefaultTransform { get; set; }

	/// <summary>Opt-in switch for the localhost AI rephrase step. Off by default.</summary>
	public bool RephraseEnabled { get; set; }

	/// <summary>The (loopback-only) Ollama endpoint used when rephrase is enabled.</summary>
	public string RephraseEndpoint { get; set; } = "http://localhost:11434";

	public static PostProcessOptions Default => new();
}
