// Wires settings persistence into the application lifecycle (WHISPER-43): on host startup it loads the
// persisted settings through the ISettingsStore port into the shared SettingsHolder (making them
// available via DI), and on graceful shutdown it writes the holder's current value back to the store.
// The concrete file-backed store lives in Infrastructure; this service is pure lifecycle orchestration
// over the port. Registered as an IHostedService so the Generic Host owns its start/stop.

using Application.Ports;
using Logic.AppManagement.Settings;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Logic.AppManagement.Lifecycle;

public sealed class SettingsLifecycleService(
	ISettingsStore store,
	SettingsHolder holder,
	ILogger<SettingsLifecycleService> logger) : IHostedService
{
	public async Task StartAsync(CancellationToken cancellationToken)
	{
		holder.Current = await store.LoadAsync(cancellationToken);
		logger.LogInformation("Loaded settings from the store on startup.");
	}

	public async Task StopAsync(CancellationToken cancellationToken)
	{
		await store.SaveAsync(holder.Current, cancellationToken);
		logger.LogInformation("Persisted settings to the store on shutdown.");
	}
}
