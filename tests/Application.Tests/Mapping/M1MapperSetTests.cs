// Verifies the M1 Mapperly mapper set (WHISPER-49) as REAL generated code (never substituted): every
// DTO <-> Domain pair round-trips without losing data and supporting value objects (HotkeyBinding) map
// faithfully. The remaining house rules are enforced where they actually live — at compile time: the
// [Mapper] partial classes only generate because they are well-formed, and "no [UseMapper] / no
// Mapperly warnings" is guaranteed by the -warnaserror build gate (Mapperly's attributes are
// compile-time only and not observable by reflection).

using Application.History;
using Application.Settings;
using Application.Statistics;
using Domain.History;
using Domain.Settings;
using Domain.Statistics;
using Xunit;

namespace Application.Tests.Mapping;

public sealed class M1MapperSetTests
{
	private static readonly DateTimeOffset When = new(2026, 1, 1, 10, 0, 0, TimeSpan.Zero);

	[Fact]
	public void Settings_round_trip_without_losing_data()
	{
		SettingsMapper mapper = new();
		AppSettings original = new("medium.en", HotkeyBinding.Parse("Ctrl+Alt+D"), 650, false);

		AppSettings roundTripped = mapper.ToDomain(mapper.ToDto(original));

		Assert.Equal(original, roundTripped);
	}

	[Fact]
	public void Settings_dto_carries_the_hotkey_as_its_canonical_chord()
	{
		SettingsMapper mapper = new();
		AppSettings original = new("base.en", HotkeyBinding.Parse("win+ctrl"), 500, true);

		AppSettingsDto dto = mapper.ToDto(original);

		Assert.Equal("Ctrl+Win", dto.Hotkey);
	}

	[Fact]
	public void Transcript_entry_round_trips_without_losing_data()
	{
		HistoryMapper mapper = new();
		TranscriptEntry original = new(Guid.NewGuid(), "schedule the meeting", When);

		TranscriptEntry roundTripped = mapper.ToDomain(mapper.ToDto(original));

		Assert.Equal(original, roundTripped);
	}

	[Fact]
	public void Usage_stats_round_trip_without_losing_data()
	{
		UsageStatsMapper mapper = new();
		UsageStats original = new(150, 3);

		UsageStats roundTripped = mapper.ToDomain(mapper.ToDto(original));

		Assert.Equal(original, roundTripped);
	}
}
