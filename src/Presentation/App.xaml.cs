// Presentation composition root. Builds the Generic Host, wiring Serilog and every layer through its
// per-layer DI extension, then resolves from that one container. There is no tray icon or window yet
// (M6) — for now the host is composed, a startup line is logged, and the app exits cleanly. This
// proves the production composition is sound and shares the exact registration extensions the BDD
// specs reuse, so the two cannot drift.

using System.Windows;
using Infrastructure.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Presentation;

public partial class App
{
	private IHost? _host;

	protected override void OnStartup(StartupEventArgs e)
	{
		base.OnStartup(e);

		HostApplicationBuilder builder = Host.CreateApplicationBuilder();
		builder.Services.AddSerilogLogging(builder.Configuration);
		builder.Services.AddWhisperServices(builder.Configuration);
		_host = builder.Build();

		ILogger<App> logger = _host.Services.GetRequiredService<ILogger<App>>();
		logger.LogInformation("Whisper host composed. Tray UI and run loop arrive in M6.");

		// No window or tray yet (M6); exit cleanly rather than sit with no UI.
		Shutdown();
	}

	protected override void OnExit(ExitEventArgs e)
	{
		_host?.Dispose();
		base.OnExit(e);
	}
}
