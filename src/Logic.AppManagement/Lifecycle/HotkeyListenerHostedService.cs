// Owns the global hotkey listener's lifetime as a hosted service, so the Generic Host
// starts the focus-independent keyboard hook when the app launches and tears it down on graceful
// shutdown. It is the first cross-cutting background component the host runs; later modules subscribe
// to the listener (wired in M7) to turn key edges into recording. Start/Stop are logged so the
// hosted-service lifecycle is observable in the application log.

using Application.Ports;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Logic.AppManagement.Lifecycle;

public sealed class HotkeyListenerHostedService(
	IHotkeyListener listener,
	ILogger<HotkeyListenerHostedService> logger) : IHostedService
{
	public Task StartAsync(CancellationToken cancellationToken)
	{
		logger.LogInformation("Starting the global hotkey listener.");
		listener.Start();
		return Task.CompletedTask;
	}

	public Task StopAsync(CancellationToken cancellationToken)
	{
		logger.LogInformation("Stopping the global hotkey listener.");
		listener.Stop();
		return Task.CompletedTask;
	}
}
