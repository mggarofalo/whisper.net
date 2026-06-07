// Mapperly mapper between WhisperModel (Domain) and WhisperModelDto (Application). Follows the house
// rules in docs/coding-standards.md: a [Mapper] partial class with compile-time generated methods,
// no [UseMapper] composition. Mapperly generates the method bodies at build time, so the mapping is
// fast, deterministic, and exercised with the real mapper in tests (never mocked).

using Application.Models;
using Domain.Models;
using Riok.Mapperly.Abstractions;

namespace Application.Mapping;

[Mapper]
public partial class WhisperModelMapper
{
	public partial WhisperModelDto ToDto(WhisperModel model);

	public partial WhisperModel ToDomain(WhisperModelDto dto);
}
