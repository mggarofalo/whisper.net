// A Whisper speech-to-text model as the domain knows it: a stable identifier, a human-friendly name,
// and its on-disk size. Used here as the representative domain type for the Mapperly mapping example;
// the full model registry behavior arrives with Module 3.

namespace Domain.Models;

public sealed record WhisperModel(string Id, string DisplayName, long SizeBytes);
