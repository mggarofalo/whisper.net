// Unit tests for SwitchActiveModelHandler (WHISPER-98). Switching the active model must do two things:
// switch the runtime lifecycle AND persist settings.ModelId — the value WhisperTranscriber loads. The
// pre-fix handler only switched the lifecycle, so dictation kept loading the default model. These pin
// the persistence (preserving the other settings) and the change broadcast, and the no-op guard.

using Application.Models;
using Application.Ports;
using Application.Settings;
using Domain.Settings;
using NSubstitute;
using Xunit;

namespace Application.Tests.Models;

public sealed class SwitchActiveModelHandlerTests
{
	private readonly IModelLifecycle _lifecycle = Substitute.For<IModelLifecycle>();
	private readonly ISettingsStore _store = Substitute.For<ISettingsStore>();
	private readonly SettingsChangeBroadcaster _broadcaster = new();

	[Fact]
	public async Task Switches_the_lifecycle_and_persists_the_selected_model_id()
	{
		AppSettings current = new("base.en", HotkeyBinding.Parse("Ctrl+Win"), 500, fillerWordRemovalEnabled: true,
			captureDeviceId: "Mic-1", auditLogEnabled: true, setupCompleted: true);
		_store.LoadAsync(Arg.Any<CancellationToken>()).Returns(current);

		AppSettings? broadcast = null;
		_broadcaster.Changed += (_, settings) => broadcast = settings;

		SwitchActiveModelHandler handler = new(_lifecycle, _store, _broadcaster);
		await handler.Handle(new SwitchActiveModelCommand("large-v3"), CancellationToken.None);

		await _lifecycle.Received(1).SwitchAsync("large-v3", Arg.Any<CancellationToken>());

		// settings.ModelId is persisted as the new model; every other setting is preserved.
		await _store.Received(1).SaveAsync(
			Arg.Is<AppSettings>(s =>
				s.ModelId == "large-v3" &&
				s.Hotkey.Chord == "Ctrl+Win" &&
				s.SilenceThresholdMs == 500 &&
				s.FillerWordRemovalEnabled &&
				s.CaptureDeviceId == "Mic-1" &&
				s.AuditLogEnabled &&
				s.SetupCompleted),
			Arg.Any<CancellationToken>());

		// The change is broadcast so the in-memory holder stays in sync (graceful shutdown won't clobber it).
		Assert.NotNull(broadcast);
		Assert.Equal("large-v3", broadcast!.ModelId);
	}

	[Fact]
	public async Task Does_not_resave_or_broadcast_when_the_model_is_already_active()
	{
		AppSettings current = new("large-v3", HotkeyBinding.Parse("Ctrl+Win"), 500, fillerWordRemovalEnabled: false);
		_store.LoadAsync(Arg.Any<CancellationToken>()).Returns(current);

		bool broadcast = false;
		_broadcaster.Changed += (_, _) => broadcast = true;

		SwitchActiveModelHandler handler = new(_lifecycle, _store, _broadcaster);
		await handler.Handle(new SwitchActiveModelCommand("large-v3"), CancellationToken.None);

		await _lifecycle.Received(1).SwitchAsync("large-v3", Arg.Any<CancellationToken>());
		await _store.DidNotReceive().SaveAsync(Arg.Any<AppSettings>(), Arg.Any<CancellationToken>());
		Assert.False(broadcast);
	}
}
