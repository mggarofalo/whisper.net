// CQRS query for the richer usage summary (WHISPER-24): transcription count, total characters and audio
// duration, plus a per-day breakdown, optionally scoped to a date range. The handler reads history
// through IHistoryStore and aggregates it via the Logic calculator.

using Application.Interfaces;

namespace Application.Statistics;

public sealed record GetUsageSummaryQuery(DateTimeOffset? From, DateTimeOffset? To) : IQuery<UsageSummaryDto>;
