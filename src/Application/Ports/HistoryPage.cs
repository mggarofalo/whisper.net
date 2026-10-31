// The result of IHistoryStore.SearchAsync: one page of matching entries (newest-first) plus the total
// number of entries that match, so callers can show "page X of Y" without reading the whole history.

using Domain.History;

namespace Application.Ports;

public sealed record HistoryPage(IReadOnlyList<TranscriptEntry> Entries, int TotalCount);
