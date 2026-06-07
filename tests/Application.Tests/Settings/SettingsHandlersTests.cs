// Unit tests for the settings handlers (WHISPER-46) in isolation, with a substituted ISettingsStore
// and the real SettingsMapper. They cover what the BDD scenarios assert at the port boundary plus the
// mapping fidelity in each direction.

using Application.Ports;
using Application.Settings;
using Domain.Settings;
using NSubstitute;
using Xunit;

namespace Application.Tests.Settings;

public sealed class SettingsHandlersTests
{
	private readonly ISettingsStore _store = Substitute.For<ISettingsStore>();
	private readonly SettingsMapper _mapper = new();

	[Fact]
	public async Task Get_returns_the_loaded_settings_as_a_dto()
	{
		AppSettings saved = new("medium.en", HotkeyBinding.Parse("Ctrl+Alt+D"), 650, false);
		_store.LoadAsync(Arg.Any<CancellationToken>()).Returns(saved);
		GetSettingsHandler handler = new(_store, _mapper);

		AppSettingsDto dto = await handler.Handle(new GetSettingsQuery(), CancellationToken.None);

		Assert.Equal("medium.en", dto.ModelId);
		Assert.Equal("Ctrl+Alt+D", dto.Hotkey);
		Assert.Equal(650, dto.SilenceThresholdMs);
		Assert.False(dto.FillerWordRemovalEnabled);
	}

	[Fact]
	public async Task Update_persists_the_mapped_domain_settings()
	{
		UpdateSettingsHandler handler = new(_store, _mapper);
		AppSettingsDto dto = new("small.en", "Win+Ctrl", 500, true);

		await handler.Handle(new UpdateSettingsCommand(dto), CancellationToken.None);

		await _store.Received(1).SaveAsync(
			Arg.Is<AppSettings>(s =>
				s.ModelId == "small.en" &&
				s.Hotkey.Chord == "Ctrl+Win" &&
				s.SilenceThresholdMs == 500 &&
				s.FillerWordRemovalEnabled),
			Arg.Any<CancellationToken>());
	}
}
