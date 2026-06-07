// CQRS query to read transcription history. Supports optional filtering by an inclusive date range and
// an optional result limit; the handler returns the matching entries newest-first as DTOs.

using Application.Interfaces;

namespace Application.History;

public sealed record QueryHistoryQuery(DateTimeOffset? From, DateTimeOffset? To, int? Limit)
	: IQuery<IReadOnlyList<TranscriptEntryDto>>;
