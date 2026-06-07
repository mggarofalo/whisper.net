// The text a transcription produced for a clip of audio. Empty text means the model heard no speech.

namespace Domain.Audio;

public sealed record TranscriptionResult(string Text);
