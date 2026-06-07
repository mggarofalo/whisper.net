// Boundary projection of a TranscriptEntry for callers (the history query and Presentation). Mirrors
// the domain shape, including the derived word count, so the Mapperly projection stays trivial.

namespace Application.History;

public sealed record TranscriptEntryDto(
	Guid Id,
	string Text,
	DateTimeOffset CreatedAt,
	int WordCount);
