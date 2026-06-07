// Mapperly mapper between UsageStats (Domain) and UsageStatsDto (Application). Per the house rules: a
// [Mapper] partial class, no [UseMapper]. Domain -> DTO is used by the usage-stats handler; the
// reverse direction completes the bidirectional contract and is exercised by the @WHISPER-49
// round-trip test. The real generated mapper is used in tests, never mocked.

using Domain.Statistics;
using Riok.Mapperly.Abstractions;

namespace Application.Statistics;

[Mapper]
public partial class UsageStatsMapper
{
	public partial UsageStatsDto ToDto(UsageStats stats);

	// EstimatedTimeSaved is derived from TotalWords inside the domain constructor, so the DTO's value
	// has no target to map onto and is explicitly ignored (the round trip recomputes it).
	[MapperIgnoreSource(nameof(UsageStatsDto.EstimatedTimeSaved))]
	public partial UsageStats ToDomain(UsageStatsDto dto);
}
