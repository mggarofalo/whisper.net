// Mapperly mapper from TranscriptEntry (Domain) to TranscriptEntryDto (Application). Per the house
// rules: a [Mapper] partial class, no [UseMapper]. Only the Domain -> DTO direction is needed here
// (the query handler projects results); the reverse mapping arrives with the WHISPER-49 mapper set.

using Domain.History;
using Riok.Mapperly.Abstractions;

namespace Application.History;

[Mapper]
public partial class HistoryMapper
{
	public partial TranscriptEntryDto ToDto(TranscriptEntry entry);
}
