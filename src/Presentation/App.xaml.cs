// Presentation composition root. Builds the Generic Host, wiring Serilog and every layer through its
// per-layer DI extension, then starts the host so it owns the application lifetime (WHISPER-12). There
// is deliberately no StartupUri and no window: launching starts the registered IHostedServices (the
// global hotkey listener today) and the process runs tray-resident — the tray icon arrives in
// WHISPER-18. Unhandled exceptions are logged before the process exits, and shutdown is graceful: the
// host's StopAsync stops every hosted service before exit. The host shares the exact registration
// extensions the BDD specs reuse, so production and test composition cannot drift.

using System.Threading.Tasks;
using System.Windows;
using Application.Ports;
using Infrastructure.DependencyInjection;
using Logic.AppManagement.Tray;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Presentation.Shell;
using Presentation.Tray;

namespace Presentation;

public partial class App
{
	private IHost? _host;
	private TrayIcon? _trayIcon;
	private bool _shuttingDown;

	protected override void OnStartup(StartupEventArgs e)
	{
		base.OnStartup(e);

		HostApplicationBuilder builder = Host.CreateApplicationBuilder();
		builder.Services.AddSerilogLogging(builder.Configuration);
		builder.Services.AddWhisperServices(builder.Configuration);

		// Tray UI (WHISPER-18): the shell presenter (settings window), the tray coordination, and its
		// view-model. Registered here in the composition root because they are Presentation concerns; the
		// controller resolves the host-provided IHostApplicationLifetime for graceful Quit.
		builder.Services.AddSingleton<IShellPresenter, WpfShellPresenter>();
		builder.Services.AddSingleton<TrayController>();
		builder.Services.AddSingleton<TrayIconViewModel>();
		_host = builder.Build();

		ILogger<App> logger = _host.Services.GetRequiredService<ILogger<App>>();
		RegisterUnhandledExceptionLogging(logger);

		// When the host's lifetime ends — e.g. a future tray Quit calls StopApplication — close the WPF
		// application so the process exits. Marshaled onto the UI thread because the callback fires on a
		// host thread; guarded so it does not re-enter a shutdown already underway via OnExit.
		IHostApplicationLifetime lifetime = _host.Services.GetRequiredService<IHostApplicationLifetime>();
		lifetime.ApplicationStopping.Register(() => Dispatcher.Invoke(ShutdownApplication));

		// Start the host: every IHostedService (the hotkey listener today) starts now. No StartupUri and
		// no window — the process runs tray-resident.
		_host.Start();

		// Create the tray icon: the user's primary entry point now that there is no window. It lives for
		// the app's lifetime and is disposed on exit.
		_trayIcon = new TrayIcon(_host.Services.GetRequiredService<TrayIconViewModel>());

		logger.LogInformation("Whisper host started; running tray-resident with no startup window.");
	}

	protected override void OnExit(ExitEventArgs e)
	{
		_trayIcon?.Dispose();

		if (_host is not null)
		{
			// Graceful shutdown: StopAsync stops every hosted service before the process exits.
			_host.StopAsync().GetAwaiter().GetResult();
			_host.Dispose();
		}

		base.OnExit(e);
	}

	private void ShutdownApplication()
	{
		if (_shuttingDown)
		{
			return;
		}

		_shuttingDown = true;
		Shutdown();
	}

	private void RegisterUnhandledExceptionLogging(ILogger<App> logger)
	{
		AppDomain.CurrentDomain.UnhandledException += (_, args) =>
			logger.LogCritical(args.ExceptionObject as Exception, "Unhandled exception; the application is terminating.");

		DispatcherUnhandledException += (_, args) =>
			logger.LogCritical(args.Exception, "Unhandled dispatcher exception.");

		TaskScheduler.UnobservedTaskException += (_, args) =>
		{
			logger.LogError(args.Exception, "Unobserved task exception.");
			args.SetObserved();
		};
	}
}
