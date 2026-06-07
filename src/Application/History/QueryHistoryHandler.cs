// Handles QueryHistoryQuery: loads the date-range-filtered entries through the IHistoryStore port, then
// enforces the query's contract — newest-first ordering and the optional result limit — and projects
// to DTOs. The ordering/limit live here (not faked behind the store) so the @WHISPER-47 scenario
// validates real handler behavior rather than the store's setup.

using Application.Interfaces;
using Application.Ports;
using Domain.History;

namespace Application.History;

public sealed class QueryHistoryHandler(IHistoryStore store, HistoryMapper mapper)
	: IQueryHandler<QueryHistoryQuery, IReadOnlyList<TranscriptEntryDto>>
{
	public async ValueTask<IReadOnlyList<TranscriptEntryDto>> Handle(QueryHistoryQuery query, CancellationToken cancellationToken)
	{
		IReadOnlyList<TranscriptEntry> entries =
			await store.GetEntriesAsync(query.From, query.To, limit: null, cancellationToken);

		return entries
			.OrderByDescending(entry => entry.CreatedAt)
			.Take(query.Limit ?? int.MaxValue)
			.Select(mapper.ToDto)
			.ToList();
	}
}
