// Wires settings persistence into the application lifecycle (WHISPER-43): on host startup it loads the
// persisted settings through the ISettingsStore port into the shared SettingsHolder (making them
// available via DI), and on graceful shutdown it writes the holder's current value back to the store.
// It also keeps the holder in sync with every settings change broadcast (WHISPER-98): handlers that
// change settings save to the store and raise SettingsChangeBroadcaster, but nothing updated the holder,
// so a graceful shutdown overwrote the store with the stale startup snapshot — silently reverting the
// model/hotkey/device the user had changed. Subscribing here closes that gap. The concrete file-backed
// store lives in Infrastructure; this service is pure lifecycle orchestration over the port. Registered
// as an IHostedService so the Generic Host owns its start/stop.

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
	public async Task StartAsync(CancellationToken cancellationToken)
	{
		holder.Current = await store.LoadAsync(cancellationToken);

		// Track committed changes on the instant-apply channel (WHISPER-78) so the value saved on shutdown is
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
		await store.SaveAsync(holder.Current, cancellationToken);
		logger.LogInformation("Persisted settings to the store on shutdown.");
	}

	private void OnSettingsChanged(AppSettings settings) => holder.Current = settings;
}
