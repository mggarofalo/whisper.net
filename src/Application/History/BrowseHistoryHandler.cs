// Handles BrowseHistoryQuery: loads the date-range-filtered entries through the IHistoryStore port, then
// enforces the query's contract — newest-first ordering, the optional case-insensitive text filter, and
// paging — and projects to DTOs. The ordering/filter/paging live here (not faked behind the store) so the
// @WHISPER-17 scenarios validate real handler behavior. The validator guarantees a positive page size and
// a page of at least one before this runs.

using Application.Interfaces;
using Application.Ports;
using Domain.History;

namespace Application.History;

public sealed class BrowseHistoryHandler(IHistoryStore store, HistoryMapper mapper)
	: IQueryHandler<BrowseHistoryQuery, IReadOnlyList<TranscriptEntryDto>>
{
	public async ValueTask<IReadOnlyList<TranscriptEntryDto>> Handle(BrowseHistoryQuery query, CancellationToken cancellationToken)
	{
		IReadOnlyList<TranscriptEntry> entries =
			await store.GetEntriesAsync(query.From, query.To, limit: null, cancellationToken);

		IEnumerable<TranscriptEntry> ordered = entries.OrderByDescending(entry => entry.CreatedAt);

		if (!string.IsNullOrWhiteSpace(query.TextFilter))
		{
			ordered = ordered.Where(entry => entry.Text.Contains(query.TextFilter, StringComparison.OrdinalIgnoreCase));
		}

		return ordered
			.Skip((query.Page - 1) * query.PageSize)
			.Take(query.PageSize)
			.Select(mapper.ToDto)
			.ToList();
	}
}
