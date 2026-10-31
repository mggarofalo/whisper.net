// CQRS query behind the History section: one 1-based page of transcripts, newest-first, optionally
// filtered by a case-insensitive text search. The filter and paging run in the store (IHistoryStore.SearchAsync),
// so a large history is never loaded whole. Paging inputs are validated by SearchHistoryQueryValidator.

using Application.Interfaces;

namespace Application.History;

public sealed record SearchHistoryQuery(string? Text, int Page, int PageSize) : IQuery<HistoryPageDto>;
