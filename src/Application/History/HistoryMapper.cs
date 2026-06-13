// Mapperly mapper between TranscriptEntry (Domain) and TranscriptEntryDto (Application). Per the house
// rules: a [Mapper] partial class, no [UseMapper]. Domain -> DTO is used by the query handler; the
// reverse direction completes the bidirectional contract and is exercised by the round-trip test.
// The real generated mapper is used in tests, never mocked.

using Domain.History;
using Riok.Mapperly.Abstractions;

namespace Application.History;

[Mapper]
public partial class HistoryMapper
{
	public partial TranscriptEntryDto ToDto(TranscriptEntry entry);

	// WordCount is derived from Text inside the domain constructor, so the DTO's WordCount has no
	// target to map onto and is explicitly ignored (the round trip recomputes it from Text).
	[MapperIgnoreSource(nameof(TranscriptEntryDto.WordCount))]
	public partial TranscriptEntry ToDomain(TranscriptEntryDto dto);
}
