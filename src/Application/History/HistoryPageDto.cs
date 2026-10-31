// One page of history as returned by SearchHistoryQuery: the page's entries (newest-first), the page it
// actually is (the handler clamps a page past the end to the last one), the page size, and the total
// number of matching entries. TotalPages is never below one, so an empty result still reads "page 1 of 1".

namespace Application.History;

public sealed record HistoryPageDto(IReadOnlyList<TranscriptEntryDto> Entries, int Page, int PageSize, int TotalCount)
{
	public int TotalPages => TotalCount == 0 ? 1 : (TotalCount + PageSize - 1) / PageSize;
}
