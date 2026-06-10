// Presentation composition root. Builds the Generic Host, wiring Serilog and every layer through its
// per-layer DI extension, then starts the host so it owns the application lifetime (WHISPER-12). There
// is deliberately no StartupUri and no window: launching starts the registered IHostedServices (the
// global hotkey listener today) and the process runs tray-resident — the tray icon arrives in
// WHISPER-18. Unhandled exceptions are logged before the process exits, and shutdown is graceful: the
// host's StopAsync stops every hosted service before exit. The host shares the exact registration
// extensions the BDD specs reuse, so production and test composition cannot drift.

using System.Threading.Tasks;
using System.Windows;
using Application.Diagnostics;
using Application.Ports;
using Application.Settings;
using Infrastructure.DependencyInjection;
using Logic.AppManagement;
using Logic.AppManagement.Diagnostics;
using Logic.AppManagement.Lifecycle;
using Logic.AppManagement.Shell;
using Logic.AppManagement.Tray;
using Mediator;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Presentation.Diagnostics;
using Presentation.Overlay;
using Presentation.Shell;
using Presentation.Tray;
using Velopack;

namespace Presentation;

public partial class App
{
	private IHost? _host;
	private TrayIcon? _trayIcon;
	private LevelOverlay? _levelOverlay;
	private bool _hostStarted;
	private bool _shuttingDown;

	protected override void OnStartup(StartupEventArgs e)
	{
		// Velopack install/update hooks (WHISPER-20): must run before anything else so that, when the
		// installer or updater launches the app with a hook argument (first-install, update, uninstall), it
		// performs the hook and exits instead of starting the tray app. On a normal launch this is a no-op.
		// The in-app auto-update check that consumes this is wired in WHISPER-29.
		VelopackApp.Build().Run();

		base.OnStartup(e);

		// Native theming (WHISPER-84): opt into WPF's built-in Fluent theme, following the OS Light/Dark
		// preference and accent colour, so the settings window looks native with no third-party dependency.
		// ThemeMode is experimental in .NET 10 (WPF0001); the opt-in is deliberate and isolated to this one
		// line so it cannot destabilize the app's logic. Rationale is recorded in docs/theming.md.
#pragma warning disable WPF0001
		ThemeMode = ThemeMode.System;
#pragma warning restore WPF0001

		// Doctor / selftest (WHISPER-50): when launched with --doctor, run the environment checks, print the
		// pass/warn/fail report to the launching terminal, set the exit code from the result, and exit
		// without going tray-resident. This is the diagnostics entry point users attach to a bug report.
		if (DoctorMode.IsRequested(e.Args))
		{
			RunDoctorAndExit(e.Args);
			return;
		}

		HostApplicationBuilder builder = Host.CreateApplicationBuilder();
		builder.Services.AddSerilogLogging(builder.Configuration);
		builder.Services.AddWhisperServices(builder.Configuration);

		// Tray UI (WHISPER-18): the shell presenter (settings window), the tray coordination, and its
		// view-model. Registered here in the composition root because they are Presentation concerns; the
		// controller resolves the host-provided IHostApplicationLifetime for graceful Quit.
		builder.Services.AddSingleton<IShellPresenter, WpfShellPresenter>();
		builder.Services.AddSingleton<TrayController>();
		builder.Services.AddSingleton<TrayIconViewModel>();

		// Level overlay (WHISPER-26): the recording-state-driven mini-recorder. The controller (Logic) and
		// its view-model are composed here in the Presentation root; the window itself is created after the
		// host starts so it lives on the UI thread.
		builder.Services.AddSingleton<LevelOverlayController>();
		builder.Services.AddSingleton<LevelOverlayViewModel>();

		// Single-instance coordination (WHISPER-25): resolves the Infrastructure lock + signal and the
		// shell presenter to surface the running instance on a second launch.
		builder.Services.AddSingleton<SingleInstanceCoordinator>();
		_host = builder.Build();

		ILogger<App> logger = _host.Services.GetRequiredService<ILogger<App>>();
		RegisterUnhandledExceptionLogging(logger);

		// Single-instance enforcement (WHISPER-25): become the sole instance, or signal the already-running
		// instance to surface and exit without starting a second host. Done before the host starts so a
		// second launch never installs a second hotkey hook or tray icon. The lock is released when the
		// host disposes the coordinator on graceful shutdown, so a later launch becomes the sole instance.
		SingleInstanceCoordinator singleInstance = _host.Services.GetRequiredService<SingleInstanceCoordinator>();
		if (!singleInstance.TryStartAsPrimary())
		{
			logger.LogInformation("Another instance is already running; activated it and exiting.");
			Shutdown();
			return;
		}

		// When the host's lifetime ends — e.g. a future tray Quit calls StopApplication — close the WPF
		// application so the process exits. Marshaled onto the UI thread because the callback fires on a
		// host thread; guarded so it does not re-enter a shutdown already underway via OnExit.
		IHostApplicationLifetime lifetime = _host.Services.GetRequiredService<IHostApplicationLifetime>();
		lifetime.ApplicationStopping.Register(() => Dispatcher.Invoke(ShutdownApplication));

		// Start the host: every IHostedService (the hotkey listener today) starts now. No StartupUri and
		// no window — the process runs tray-resident.
		_host.Start();
		_hostStarted = true;

		// Create the tray icon: the user's primary entry point now that there is no window. It lives for
		// the app's lifetime and is disposed on exit.
		_trayIcon = new TrayIcon(_host.Services.GetRequiredService<TrayIconViewModel>());

		// The level overlay lives for the app's lifetime, hidden until recording starts (WHISPER-26).
		_levelOverlay = new LevelOverlay(_host.Services.GetRequiredService<LevelOverlayViewModel>());

		logger.LogInformation("Whisper host started; running tray-resident with no startup window.");

		// First-run setup (WHISPER-82): there is no separate onboarding window any more — settings IS the
		// single source of truth. On launch, open the settings window when the app is unconfigured (no
		// active model OR setup not completed) so the user finishes setup over the real settings views;
		// otherwise stay tray-only. Fire-and-forget so the settings/cache read runs off the UI thread
		// instead of blocking it with sync-over-async; the continuation resumes on the UI thread to show
		// the window via the shell presenter.
		_ = OpenSettingsIfUnconfiguredAsync(logger);
	}

	// Builds the full composition (but never starts the host, so no hotkey hook / tray / hosted services
	// run), sends the diagnostics query through the same Mediator pipeline the rest of the app uses, and
	// prints the formatted report. The exit code is non-zero when any check failed, so a script or CI step
	// can detect a broken environment. Runs synchronously on the UI thread — this path never shows a window.
	private void RunDoctorAndExit(string[] args)
	{
		HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);
		builder.Services.AddSerilogLogging(builder.Configuration);
		builder.Services.AddWhisperServices(builder.Configuration);

		using IHost host = builder.Build();
		using IServiceScope scope = host.Services.CreateScope();

		IMediator mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
		DiagnosticReport report = mediator.Send(new RunDiagnosticsQuery()).AsTask().GetAwaiter().GetResult();

		ConsoleOutput.WriteLine(DiagnosticReportFormatter.Format(report));

		Environment.ExitCode = report.Overall == DiagnosticStatus.Fail ? 1 : 0;
		Shutdown();
	}

	private async Task OpenSettingsIfUnconfiguredAsync(ILogger<App> logger)
	{
		SetupStatus status;
		try
		{
			// A short-lived scope for the one-shot query; awaited (not GetAwaiter().GetResult()) so the
			// settings/cache read does not block the UI thread, with the continuation resuming on the WPF
			// dispatcher so the window is shown on the UI thread.
			using IServiceScope scope = _host!.Services.CreateScope();
			IMediator mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
			status = await mediator.Send(new GetSetupStatusQuery());
		}
		catch (Exception ex)
		{
			// A failure deciding whether the app is configured must not strand it; log it and stay tray-only.
			logger.LogError(ex, "Failed to determine whether first-run setup is required; continuing tray-only.");
			return;
		}

		if (status.IsConfigured)
		{
			return;
		}

		logger.LogInformation("App is unconfigured; opening the settings window for first-run setup.");
		_host!.Services.GetRequiredService<IShellPresenter>().ShowSettings();
	}

	protected override void OnExit(ExitEventArgs e)
	{
		_levelOverlay?.Dispose();
		_trayIcon?.Dispose();

		if (_host is not null)
		{
			// Graceful shutdown: StopAsync stops every hosted service before the process exits. Skipped if
			// the host never started (a second instance that exited after activating the primary); disposing
			// the host still releases the single-instance lock and stops the activation listener.
			if (_hostStarted)
			{
				_host.StopAsync().GetAwaiter().GetResult();
			}

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
		{
			// Record the failure (now to the persistent file sink, WHISPER-73) and keep the tray app alive
			// instead of letting an error in a single UI callback tear the whole process down with no trace —
			// which is what made the onboarding window appear to "just close". Marking it handled stops the
			// default terminate; a genuinely fatal error still surfaces via AppDomain.UnhandledException.
			logger.LogCritical(args.Exception, "Unhandled dispatcher exception; the UI action was aborted but the app keeps running.");
			args.Handled = true;
		};

		TaskScheduler.UnobservedTaskException += (_, args) =>
		{
			logger.LogError(args.Exception, "Unobserved task exception.");
			args.SetObserved();
		};
	}
}
