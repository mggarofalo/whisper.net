// Handles GetUsageStatsQuery: loads the date-range-filtered history through the IHistoryStore port,
// delegates the aggregation math to the Logic calculator, and projects the result to a DTO. Pure
// orchestration — no aggregation logic lives here.

using Application.Interfaces;
using Application.Ports;
using Domain.History;
using Domain.Statistics;

namespace Application.Statistics;

public sealed class GetUsageStatsHandler(
	IHistoryStore store,
	IUsageStatsCalculator calculator,
	UsageStatsMapper mapper)
	: IQueryHandler<GetUsageStatsQuery, UsageStatsDto>
{
	public async ValueTask<UsageStatsDto> Handle(GetUsageStatsQuery query, CancellationToken cancellationToken)
	{
		IReadOnlyList<TranscriptEntry> entries =
			await store.GetEntriesAsync(query.From, query.To, limit: null, cancellationToken);

		UsageStats stats = calculator.Aggregate(entries);
		return mapper.ToDto(stats);
	}
}
