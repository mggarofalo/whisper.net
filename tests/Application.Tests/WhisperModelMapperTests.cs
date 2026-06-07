// Verifies the representative Mapperly mapping (WHISPER-56) using the REAL mapper — never a mock, per
// the house rules: a real Mapperly mapper is fast and deterministic, so mocking it would only hide
// mapping bugs. Round-trips a domain entity to its DTO and back and asserts every field survives.

using Application.Mapping;
using Application.Models;
using Domain.Models;
using Xunit;

namespace Application.Tests;

public sealed class WhisperModelMapperTests
{
	private readonly WhisperModelMapper _mapper = new();

	[Fact]
	public void Maps_every_field_from_domain_to_dto()
	{
		WhisperModel model = new("large-v3", "Large v3", 3_090_000_000);

		WhisperModelDto dto = _mapper.ToDto(model);

		Assert.Equal(model.Id, dto.Id);
		Assert.Equal(model.DisplayName, dto.DisplayName);
		Assert.Equal(model.SizeBytes, dto.SizeBytes);
	}

	[Fact]
	public void Round_trips_without_losing_data()
	{
		WhisperModel original = new("base.en", "Base (English)", 142_000_000);

		WhisperModel roundTripped = _mapper.ToDomain(_mapper.ToDto(original));

		Assert.Equal(original, roundTripped);
	}
}
