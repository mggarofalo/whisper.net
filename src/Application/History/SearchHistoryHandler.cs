// Handles SearchHistoryQuery: asks the IHistoryStore port for one page of matching entries plus the total
// match count, and projects to DTOs. Blank search text means "no filter". A page past the end (the history
// shrank, or a search narrowed it) is clamped to the last page with one extra store read, so the caller
// never lands on an empty page while matches exist. The validator guarantees a page of at least one and a
// sane page size before this runs.

using Application.Interfaces;
using Application.Ports;

namespace Application.History;

public sealed class SearchHistoryHandler(IHistoryStore store, HistoryMapper mapper)
	: IQueryHandler<SearchHistoryQuery, HistoryPageDto>
{
	public async ValueTask<HistoryPageDto> Handle(SearchHistoryQuery query, CancellationToken cancellationToken)
	{
		string? text = string.IsNullOrWhiteSpace(query.Text) ? null : query.Text.Trim();
		int page = query.Page;

		HistoryPage result = await store.SearchAsync(text, (page - 1) * query.PageSize, query.PageSize, cancellationToken);

		if (result.Entries.Count == 0 && result.TotalCount > 0 && page > 1)
		{
			page = (result.TotalCount + query.PageSize - 1) / query.PageSize;
			result = await store.SearchAsync(text, (page - 1) * query.PageSize, query.PageSize, cancellationToken);
		}

		return new HistoryPageDto(
			result.Entries.Select(mapper.ToDto).ToList(),
			page,
			query.PageSize,
			result.TotalCount);
	}
}
