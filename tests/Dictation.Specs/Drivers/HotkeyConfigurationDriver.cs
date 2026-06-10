// Drives the @WHISPER-75 hotkey-reassignment scenarios. It builds the REAL HotkeyConfigurationHostedService
// over the REAL HotkeyActivationController and the REAL Mediator pipeline (GetSettings / UpdateSettings,
// whose handler publishes on the instant-apply IMessenger channel), faking only the settings store with a
// round-trip so a save is reflected in the next load. It proves the two halves of the fix: the controller is
// configured from the persisted hotkey at startup, and a change pushed through UpdateSettingsCommand rebinds
// the live matcher immediately — without an app restart. The thin settings/onboarding views are Presentation
// glue. The messenger is the SAME DI-resolved singleton the update handler's channel publishes on.

using Application.Settings;
using AwesomeAssertions;
using CommunityToolkit.Mvvm.Messaging;
using Domain.Settings;
using Logic.AppManagement;
using Logic.AppManagement.Lifecycle;
using Mediator;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace Dictation.Specs.Drivers;

public sealed class HotkeyConfigurationDriver
{
	private readonly IMediator _mediator;
	private readonly HotkeyActivationController _controller;
	private readonly HotkeyConfigurationHostedService _service;

	private AppSettings _persisted = AppSettings.Default;

	public HotkeyConfigurationDriver(
		IMediator mediator,
		Application.Ports.ISettingsStore store,
		HotkeyActivationController controller,
		IMessenger messenger)
	{
		_mediator = mediator;
		_controller = controller;

		// Round-trip the store so a save is visible to the next load (the controller's startup config).
		store.LoadAsync(Arg.Any<CancellationToken>()).Returns(_ => _persisted);
		store.When(s => s.SaveAsync(Arg.Any<AppSettings>(), Arg.Any<CancellationToken>()))
			.Do(call => _persisted = call.Arg<AppSettings>());

		_service = new HotkeyConfigurationHostedService(
			controller, store, messenger, NullLogger<HotkeyConfigurationHostedService>.Instance);
	}

	// --- given / when ---

	// Persist a hotkey through the real pipeline (validated, saved, published) before the service starts —
	// so startup config reads it. The published change has no live effect yet because nothing is registered.
	public async Task PersistHotkey(string chord) => await ChangeHotkey(chord);

	public async Task StartPipeline() => await _service.StartAsync(CancellationToken.None);

	// Change the hotkey through the real UpdateSettingsCommand; its handler publishes on the messenger, which
	// the started service registered on, so the live controller rebinds.
	public async Task ChangeHotkey(string chord)
	{
		AppSettingsDto current = await _mediator.Send(new GetSettingsQuery());
		await _mediator.Send(new UpdateSettingsCommand(current with { Hotkey = chord }));
	}

	// --- then ---

	public void AssertControllerMatchesChord(string chord) =>
		_controller.Binding.Chord.Should().Be(HotkeyBinding.Parse(chord).Chord);
}
