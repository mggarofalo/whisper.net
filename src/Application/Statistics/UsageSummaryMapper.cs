// Mapperly mapper from UsageSummary (Domain) to UsageSummaryDto (Application). Per the house rules: a
// [Mapper] partial class, no [UseMapper]. Only the Domain -> DTO direction is needed (the summary is
// read-only output for the dashboard); the nested DailyUsage -> DailyUsageDto element mapping is declared
// so Mapperly maps the per-day list. The real generated mapper is used in tests, never mocked.

using Domain.Statistics;
using Riok.Mapperly.Abstractions;

namespace Application.Statistics;

[Mapper]
public partial class UsageSummaryMapper
{
	public partial UsageSummaryDto ToDto(UsageSummary summary);

	private partial DailyUsageDto ToDto(DailyUsage day);
}
