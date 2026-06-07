// CQRS query for the dashboard's aggregate usage statistics, optionally scoped to a date range. The
// handler reads history through IHistoryStore and aggregates it via the Logic calculator.

using Application.Interfaces;

namespace Application.Statistics;

public sealed record GetUsageStatsQuery(DateTimeOffset? From, DateTimeOffset? To) : IQuery<UsageStatsDto>;
