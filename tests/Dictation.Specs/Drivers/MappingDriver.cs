// Drives the mapping round-trip scenarios using the REAL Mapperly mappers resolved from DI (never
// substituted — a generated mapper is deterministic, so mocking it would only hide mapping bugs). It
// maps a domain object to its DTO and back and captures both ends for the equality assertion.
// Scenario-scoped.

using Application.History;
using Application.Settings;
using AwesomeAssertions;
using Domain.History;
using Domain.Settings;

namespace Dictation.Specs.Drivers;

public sealed class MappingDriver(HistoryMapper historyMapper, SettingsMapper settingsMapper)
{
	private object? _original;
	private object? _roundTripped;

	public void GivenATranscriptEntry() =>
		_original = new TranscriptEntry(Guid.NewGuid(), "schedule the meeting", new DateTimeOffset(2026, 1, 1, 10, 0, 0, TimeSpan.Zero));

	public void GivenAppSettings() =>
		_original = new AppSettings("small.en", HotkeyBinding.Parse("Ctrl+Shift+D"), silenceThresholdMs: 700, fillerWordRemovalEnabled: false);

	public void RoundTrip()
	{
		switch (_original)
		{
			case TranscriptEntry entry:
				_roundTripped = historyMapper.ToDomain(historyMapper.ToDto(entry));
				break;
			case AppSettings settings:
				_roundTripped = settingsMapper.ToDomain(settingsMapper.ToDto(settings));
				break;
			default:
				throw new InvalidOperationException("No domain object was arranged for the round trip.");
		}
	}

	public void AssertRoundTripEqualsOriginal() =>
		_roundTripped.Should().Be(_original);
}
