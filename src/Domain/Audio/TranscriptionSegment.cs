// One timed slice of a transcription: the recognized text for a span of the clip, with its start/end
// offsets and the model's confidence in it. Whisper.net exposes these per segment; carrying them lets
// the rest of the app reason about timing and confidence (e.g. drop low-confidence output) rather than
// seeing only a flat string.

namespace Domain.Audio;

public sealed record TranscriptionSegment(string Text, TimeSpan Start, TimeSpan End, float Confidence);
