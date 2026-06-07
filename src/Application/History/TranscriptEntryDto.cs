// Boundary projection of a TranscriptEntry for callers (the history query and Presentation). Mirrors
// the domain shape, including the captured audio duration and the derived word count, so the Mapperly
// projection stays trivial.

namespace Application.History;

public sealed record TranscriptEntryDto(
	Guid Id,
	string Text,
	DateTimeOffset CreatedAt,
	int WordCount,
	TimeSpan AudioDuration = default);
