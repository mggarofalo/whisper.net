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
using Presentation.Onboarding;
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
	private OnboardingWindow? _onboardingWindow;
	private IServiceScope? _onboardingScope;
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

		// First-run onboarding (WHISPER-51): on a fresh install (setup not completed), guide the user
		// through model/audio/hotkey setup and permissions before the tray app takes over. The flow's
		// view-models depend on the scoped Mediator, so it runs inside a dedicated UI scope kept alive
		// until the window closes. Skipped silently once setup has been completed. Fire-and-forget so the
		// "is onboarding required?" settings read (WHISPER-73) runs off the UI thread instead of blocking
		// it with sync-over-async; the async continuation resumes on the UI thread to show the window.
		_ = ShowOnboardingIfRequiredAsync(logger);
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

	private async Task ShowOnboardingIfRequiredAsync(ILogger<App> logger)
	{
		_onboardingScope = _host!.Services.CreateScope();
		OnboardingViewModel onboarding = _onboardingScope.ServiceProvider.GetRequiredService<OnboardingViewModel>();

		bool required;
		try
		{
			// Awaited (not GetAwaiter().GetResult()) so the settings read does not block the UI thread; the
			// continuation resumes on the WPF dispatcher, so the window is still created on the UI thread.
			required = await onboarding.IsRequiredAsync();
		}
		catch (Exception ex)
		{
			// A failure deciding whether onboarding is needed must not strand the app; log it (now to the
			// persistent file sink) and fall through to the normal tray experience.
			logger.LogError(ex, "Failed to determine whether onboarding is required; continuing without it.");
			_onboardingScope.Dispose();
			_onboardingScope = null;
			return;
		}

		if (!required)
		{
			_onboardingScope.Dispose();
			_onboardingScope = null;
			return;
		}

		logger.LogInformation("First run detected; showing onboarding.");
		_onboardingWindow = new OnboardingWindow(onboarding);
		_onboardingWindow.Show();
	}

	protected override void OnExit(ExitEventArgs e)
	{
		_onboardingScope?.Dispose();
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
