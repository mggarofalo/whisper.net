// Data-transfer projection of a WhisperModel for crossing the Application boundary (e.g. to the
// Presentation layer). Kept structurally aligned with the domain type so the Mapperly mapping stays
// trivial and warning-free.

namespace Application.Models;

public sealed record WhisperModelDto(string Id, string DisplayName, long SizeBytes);
