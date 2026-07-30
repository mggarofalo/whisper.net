// Wires settings persistence into the application lifecycle: on host startup it loads the
// persisted settings through the ISettingsStore port into the shared SettingsHolder (making them
// available via DI), and on graceful shutdown it writes the holder's current value back to the store.
// It also keeps the holder in sync with every settings change broadcast: handlers that
// change settings save to the store and raise SettingsChangeBroadcaster, but nothing updated the holder,
// so a graceful shutdown overwrote the store with the stale startup snapshot — silently reverting the
// model/hotkey/device the user had changed. Subscribing here closes that gap. The concrete file-backed
// store lives in Infrastructure; this service is pure lifecycle orchestration over the port.  Registered
// as an IHostedService so the Generic Host owns its start/stop.
//
// The shutdown save is CONDITIONAL, and that matters. Every settings change already persists itself
// eagerly through its handler, so this save is only a safety net — but as an unconditional one it was
// actively destructive: when a startup load failed for any transient reason (the store locked by another
// process, a partially written file, an unreadable profile) the port recovers with AppSettings.Default,
// and shutdown then wrote those defaults over the user's real, intact settings. One unlucky launch
// silently reset the model, hotkey, and capture device. Saving only when a change was actually observed
// keeps the safety net and removes the destructive path: with no change to persist there is nothing worth
// risking the stored document for.

using Application.Ports;
using Application.Settings;
using CommunityToolkit.Mvvm.Messaging;
using Domain.Settings;
using Logic.AppManagement.Settings;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Logic.AppManagement.Lifecycle;

public sealed class SettingsLifecycleService(
	ISettingsStore store,
	SettingsHolder holder,
	IMessenger messenger,
	ILogger<SettingsLifecycleService> logger) : IHostedService
{
	// Whether a settings change was observed since startup. Guards the shutdown save so a session that
	// changed nothing can never overwrite the stored document with the snapshot it loaded.
	private bool _observedChange;

	public async Task StartAsync(CancellationToken cancellationToken)
	{
		holder.Current = await store.LoadAsync(cancellationToken);

		// Track committed changes on the instant-apply channel so the value saved on shutdown is
		// the latest one the user chose — not the snapshot loaded at startup. Weak registration, so no leak.
		messenger.Register<SettingsLifecycleService, SettingsChangedMessage>(
			this, static (recipient, message) => recipient.OnSettingsChanged(message.Value));
		logger.LogInformation("Loaded settings from the store on startup.");
	}

	public async Task StopAsync(CancellationToken cancellationToken)
	{
		// Stop tracking before the final save: once we have written the holder's value, a later change must
		// not re-dirty it. (Registration is weak, so this is for correctness, not leak prevention.)
		messenger.UnregisterAll(this);

		if (!_observedChange)
		{
			// Nothing changed this session, so the store already holds the authoritative value. Writing the
			// loaded snapshot back could only ever do harm — it would persist a recovered default over good
			// settings if the startup load had fallen back.
			logger.LogInformation("No settings changed this session; leaving the stored settings untouched.");
			return;
		}

		await store.SaveAsync(holder.Current, cancellationToken);
		logger.LogInformation("Persisted settings to the store on shutdown.");
	}

	private void OnSettingsChanged(AppSettings settings)
	{
		holder.Current = settings;
		_observedChange = true;
	}
}
