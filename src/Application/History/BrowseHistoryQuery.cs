// CQRS query to browse transcription history a page at a time (WHISPER-17). Supports paging (1-based
// page over a page size), most-recent-first ordering, and optional text/date filtering. Returns the
// matching page of entries as DTOs. Paging inputs are validated by BrowseHistoryQueryValidator before
// the handler runs.

using Application.Interfaces;

namespace Application.History;

public sealed record BrowseHistoryQuery(
	int PageSize,
	int Page,
	string? TextFilter = null,
	DateTimeOffset? From = null,
	DateTimeOffset? To = null) : IQuery<IReadOnlyList<TranscriptEntryDto>>;
