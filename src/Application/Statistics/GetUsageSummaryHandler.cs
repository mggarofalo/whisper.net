// Handles GetUsageSummaryQuery: loads the date-range-filtered history through the IHistoryStore port,
// delegates the aggregation math (totals + per-day breakdown) to the Logic calculator, and projects the
// result to a DTO. Pure orchestration — no aggregation logic lives here.

using Application.Interfaces;
using Application.Ports;
using Domain.History;
using Domain.Statistics;

namespace Application.Statistics;

public sealed class GetUsageSummaryHandler(
	IHistoryStore store,
	IUsageStatsCalculator calculator,
	UsageSummaryMapper mapper)
	: IQueryHandler<GetUsageSummaryQuery, UsageSummaryDto>
{
	public async ValueTask<UsageSummaryDto> Handle(GetUsageSummaryQuery query, CancellationToken cancellationToken)
	{
		IReadOnlyList<TranscriptEntry> entries =
			await store.GetEntriesAsync(query.From, query.To, limit: null, cancellationToken);

		UsageSummary summary = calculator.Summarize(entries);
		return mapper.ToDto(summary);
	}
}
