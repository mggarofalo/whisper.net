// Drives the settings scenarios through the REAL Mediator pipeline (validation behavior + handlers +
// Mapperly mapper), substituting only the ISettingsStore port. It sends GetSettingsQuery /
// UpdateSettingsCommand and asserts at the port boundary (loaded from / written to the store) or on
// the rejection. Scenario-scoped, so its captured state is fresh per scenario.

using Application.Ports;
using Application.Settings;
using AwesomeAssertions;
using Domain.Settings;
using FluentValidation;
using Mediator;
using NSubstitute;

namespace Dictation.Specs.Drivers;

public sealed class SettingsDriver(IMediator mediator, ISettingsStore store)
{
	// A representative saved settings value used by the "current settings are returned" scenario.
	private static readonly AppSettings Saved =
		new("small.en", HotkeyBinding.Parse("Ctrl+Shift+D"), silenceThresholdMs: 700, fillerWordRemovalEnabled: false);

	private AppSettingsDto? _loaded;
	private AppSettingsDto? _pendingUpdate;
	private bool _rejected;

	public void StoreHoldsSavedSettings() =>
		store.LoadAsync(Arg.Any<CancellationToken>()).Returns(Saved);

	public async Task RequestCurrentSettings() =>
		_loaded = await mediator.Send(new GetSettingsQuery());

	public void AssertSavedSettingsReturned()
	{
		_loaded.Should().NotBeNull();
		_loaded!.ModelId.Should().Be(Saved.ModelId);
		_loaded.Hotkey.Should().Be(Saved.Hotkey.Chord);
		_loaded.SilenceThresholdMs.Should().Be(Saved.SilenceThresholdMs);
		_loaded.FillerWordRemovalEnabled.Should().Be(Saved.FillerWordRemovalEnabled);
	}

	public void PrepareValidUpdate() =>
		_pendingUpdate = new AppSettingsDto("base.en", "Ctrl+Win", SilenceThresholdMs: 500, FillerWordRemovalEnabled: true);

	public void PrepareUpdateWithUnknownModel() =>
		_pendingUpdate = new AppSettingsDto("totally-not-a-model", "Ctrl+Win", SilenceThresholdMs: 500, FillerWordRemovalEnabled: true);

	public Task SubmitUpdate() => Submit(_pendingUpdate!);

	public void AssertSettingsWereSaved() =>
		store.Received(1).SaveAsync(Arg.Any<AppSettings>(), Arg.Any<CancellationToken>());

	public void AssertRejectedAndNothingSaved()
	{
		_rejected.Should().BeTrue();
		store.DidNotReceive().SaveAsync(Arg.Any<AppSettings>(), Arg.Any<CancellationToken>());
	}

	// Sends the command, recording whether the validation pipeline rejected it before the handler ran.
	private async Task Submit(AppSettingsDto settings)
	{
		try
		{
			await mediator.Send(new UpdateSettingsCommand(settings));
			_rejected = false;
		}
		catch (ValidationException)
		{
			_rejected = true;
		}
	}
}
