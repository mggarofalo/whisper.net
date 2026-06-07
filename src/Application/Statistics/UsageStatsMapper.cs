// Mapperly mapper from UsageStats (Domain) to UsageStatsDto (Application). Per the house rules: a
// [Mapper] partial class, no [UseMapper]. A trivial 1:1 projection; the real generated mapper is
// exercised in tests, never mocked.

using Domain.Statistics;
using Riok.Mapperly.Abstractions;

namespace Application.Statistics;

[Mapper]
public partial class UsageStatsMapper
{
	public partial UsageStatsDto ToDto(UsageStats stats);
}
