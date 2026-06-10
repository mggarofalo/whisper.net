// Keeps the live hotkey matcher in sync with the persisted settings (WHISPER-75; instant-apply via
// IMessenger in WHISPER-78). The HotkeyActivationController starts on the compile-time default binding and
// is never otherwise told what the user actually chose; this service closes that gap. On startup it
// configures the controller from the persisted hotkey (so a changed binding survives a restart), and it
// registers WEAKLY on the instant-apply channel (WeakReferenceMessenger) for SettingsChangedMessage so a
// committed settings edit re-binds the matcher IMMEDIATELY — the old chord stops triggering and the new one
// starts without an app restart. Weak registration means no leak and no manual unsubscribe: the host owns
// the singleton for the app's lifetime, and the messenger drops the recipient automatically when it dies.
// The activation mode is preserved (it is not a persisted setting today). Singleton dependencies only, so
// the Generic Host can own it directly.

using Application.Ports;
using Application.Settings;
using CommunityToolkit.Mvvm.Messaging;
using Domain.Settings;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Logic.AppManagement.Lifecycle;

public sealed class HotkeyConfigurationHostedService(
	HotkeyActivationController controller,
	ISettingsStore store,
	IMessenger messenger,
	ILogger<HotkeyConfigurationHostedService> logger) : IHostedService
{
	public async Task StartAsync(CancellationToken cancellationToken)
	{
		AppSettings settings = await store.LoadAsync(cancellationToken);
		Apply(settings);
		logger.LogInformation("Hotkey matcher bound to {Chord} from persisted settings.", settings.Hotkey.Chord);

		// Weak registration: no manual unsubscribe needed, no leak. The static handler keeps no captured
		// state, so the only reference the messenger holds back to this service is weak.
		messenger.Register<HotkeyConfigurationHostedService, SettingsChangedMessage>(
			this, static (recipient, message) => recipient.OnSettingsChanged(message.Value));
	}

	public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

	private void OnSettingsChanged(AppSettings settings)
	{
		Apply(settings);
		logger.LogInformation("Hotkey matcher rebound to {Chord} after a settings change.", settings.Hotkey.Chord);
	}

	// Point the controller at the persisted binding, preserving the current activation mode.
	private void Apply(AppSettings settings) => controller.Configure(settings.Hotkey, controller.Mode);
}
