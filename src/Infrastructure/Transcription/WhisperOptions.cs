// Configuration for the Whisper.net transcriber: which model file to load and which language to
// transcribe in. ModelPath is a local file path only — transcription never fetches it over the
// network (acquiring models is a separate, explicit, opt-in step). An empty/"auto" Language enables
// Whisper's automatic language detection.

namespace Infrastructure.Transcription;

public sealed class WhisperOptions
{
	public const string SectionName = "Whisper";

	/// <summary>Local path to the GGUF/ggml model file. Empty until a model has been acquired.</summary>
	public string ModelPath { get; set; } = string.Empty;

	/// <summary>Language code (e.g. "en"); empty or "auto" enables automatic detection.</summary>
	public string Language { get; set; } = "auto";

	/// <summary>
	/// User-supplied terms/phrases (names, jargon) the decoder is biased toward via prompt-token
	/// conditioning. Read fresh on each transcription, so an edited vocabulary takes effect
	/// on the next utterance without reloading the model. Empty by default — no conditioning.
	/// </summary>
	public IReadOnlyList<string> CustomVocabulary { get; set; } = [];
}
