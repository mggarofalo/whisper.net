// The text a transcription produced for a clip of audio. Empty text means the model heard no speech.
// Segments carry the per-span timing/confidence the model exposed; they are optional so callers that
// only need the recognized text (and the many places that construct a result from a bare string) are
// unaffected.

namespace Domain.Audio;

public sealed record TranscriptionResult(string Text, IReadOnlyList<TranscriptionSegment>? Segments = null);
