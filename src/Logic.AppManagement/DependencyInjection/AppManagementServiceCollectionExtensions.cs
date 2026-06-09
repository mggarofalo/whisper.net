// Per-layer DI registration for Logic.AppManagement. This is the composition seam the Generic Host
// and the BDD specs call; it registers the real app-management behaviors so specs exercise them for
// real (only Infrastructure ports are faked).

using Application.Delivery;
using Application.Ports;
using Logic.AppManagement.Lifecycle;
using Logic.AppManagement.Settings;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Logic.AppManagement.DependencyInjection;

public static class AppManagementServiceCollectionExtensions
{
	public static IServiceCollection AddAppManagement(this IServiceCollection services, IConfiguration? configuration = null)
	{
		services.AddSingleton<IUsageStatsCalculator, UsageStatsCalculator>();

		// Delivery-strategy selection (WHISPER-8): pure override-vs-default precedence for each delivery.
		services.AddSingleton<IDeliveryStrategySelector, DeliveryStrategySelector>();

		// Hotkey activation (WHISPER-16): the stateful chord matcher that turns key edges into recording
		// start/stop requests for push-to-talk and toggle. Singleton so it holds the live chord state the
		// orchestration layer (M7) subscribes to and drives from the hotkey listener.
		services.AddSingleton<HotkeyActivationController>();

		// Recording state machine (WHISPER-22): the single authority over Idle/Recording/Transcribing and
		// the Esc cancel. Singleton so the tray/UI and orchestration share one observable state.
		services.AddSingleton<RecordingStateMachine>();

		// Capture buffering options (WHISPER-14): the per-app preroll / max-duration / target-rate the
		// orchestrator builds its CaptureBuffer from. A plain default today; bound from configuration later.
		services.AddSingleton(new Logic.AudioManagement.AudioBufferingOptions());

		// Dictation orchestrator (WHISPER-14): the coordination hub that runs capture -> transcribe ->
		// deliver end to end. Scoped so it shares one Mediator scope with the handlers it dispatches (the
		// source-generated Mediator is scoped); the host activates it for the app lifetime via the hosted
		// service below, and the BDD specs resolve it per scenario from the same scope as the faked ports.
		services.AddScoped<DictationOrchestrator>();

		// Command-mode hook (WHISPER-35): the default matcher recognizes nothing, so every transcript falls
		// through to normal text delivery until a real command catalogue/execution is implemented.
		services.AddSingleton<Application.Ports.ICommandMatcher, NoOpCommandMatcher>();

		// Hotkey capture + rebinding (WHISPER-30): the one-shot capture-next-key helper that rebinds the
		// activation controller atomically. Singleton so it shares the live controller it rebinds.
		services.AddSingleton<HotkeyCaptureService>();

		// Current settings (WHISPER-43): the in-memory holder loaded on startup and saved on shutdown.
		// Singleton so every consumer shares one live view of the settings.
		services.AddSingleton<SettingsHolder>();

		// Opt-in audit log gate (WHISPER-34): reads the live settings holder so enabling/disabling auditing
		// takes effect without a restart; writes go to the local IAuditLog only when the user has opted in.
		services.AddSingleton<Audit.AuditLogger>();

		// Output transforms (WHISPER-37): the registry of named transforms (the built-in formats) and the
		// service that applies one by name via the rephrase port. A pure framework — the AI execution stays
		// behind IRephraseClient, so no Infrastructure/network type leaks into this layer.
		// Construct the registry explicitly (its default ctor seeds the built-ins). A plain AddSingleton
		// would let the container pick the IEnumerable<OutputTransform> ctor and bind it to an empty set.
		services.AddSingleton(_ => new OutputTransforms.OutputTransformRegistry());
		services.AddSingleton<OutputTransforms.OutputTransformService>();

		// Post-process pipeline (WHISPER-41): the ordered normalize -> optional transform pipeline behind
		// the IPostProcessor port, reading the live PostProcessSettingsHolder so edits apply next call.
		services.AddSingleton<Application.Ports.IPostProcessor, PostProcessing.PostProcessPipeline>();

		// Dashboard shell + navigation (WHISPER-19): the navigation service, the shell view-model, and the
		// feature view-models it hosts — all WPF-free so the specs drive the real MVVM + Mediator behavior.
		// Scoped (not singleton) because the feature view-models depend on the scoped Mediator, so the shell
		// runs inside one UI scope — like the orchestrator — never the root. Feature view-models are
		// transient: each navigation resolves a fresh instance and the previous is deactivated/disposed.
		// Sections are registered in display order; the shell opens on the first (Home).
		services.AddScoped<Shell.INavigationService, Shell.NavigationService>();
		services.AddScoped<Shell.ShellViewModel>();
		services.AddSingleton(new Shell.NavigationSection("Home", typeof(Shell.HomeViewModel)));
		services.AddSingleton(new Shell.NavigationSection("Model", typeof(Shell.ModelViewModel)));
		services.AddSingleton(new Shell.NavigationSection("Audio", typeof(Shell.AudioDeviceViewModel)));
		services.AddSingleton(new Shell.NavigationSection("Hotkey", typeof(Shell.HotkeyViewModel)));
		services.AddSingleton(new Shell.NavigationSection("History", typeof(Shell.HistoryViewModel)));
		services.AddSingleton(new Shell.NavigationSection("Stats", typeof(Shell.StatsViewModel)));
		services.AddTransient<Shell.HomeViewModel>();
		services.AddTransient<Shell.ModelViewModel>();
		services.AddTransient<Shell.AudioDeviceViewModel>();
		services.AddTransient<Shell.HotkeyViewModel>();
		services.AddTransient<Shell.HistoryViewModel>();
		services.AddTransient<Shell.StatsViewModel>();

		// First-run onboarding (WHISPER-51): not a nav section — the host shows it instead of the shell on
		// first launch. Transient so each run starts fresh.
		services.AddTransient<Shell.OnboardingViewModel>();

		// Self-diagnostics (WHISPER-50): the doctor / selftest checks, each probing one subsystem through
		// the existing ports. Registered in the order they appear in the report so the output is
		// deterministic; the aggregation lives in the Application handler, so one failing check never stops
		// the others. Scoped (not singleton) so a check can depend on the scoped store/cache ports the specs
		// substitute without a captive-dependency violation.
		services.AddScoped<Application.Diagnostics.IDiagnosticCheck, Diagnostics.AudioCaptureCheck>();
		services.AddScoped<Application.Diagnostics.IDiagnosticCheck, Diagnostics.ModelCacheCheck>();
		services.AddScoped<Application.Diagnostics.IDiagnosticCheck, Diagnostics.HotkeyCheck>();
		services.AddScoped<Application.Diagnostics.IDiagnosticCheck, Diagnostics.GpuCheck>();

		// Auto-update policy (WHISPER-29): decides check/download/apply and degrades gracefully on failure.
		// Pure policy over the IUpdateSource port; the startup hosted service below drives it.
		services.AddSingleton<Updates.AutoUpdateService>();

		return services;
	}

	// The host-owned background components (WHISPER-12). Kept separate from AddAppManagement so the BDD
	// scenario container — which composes the inner layers but never runs a Generic Host — is not forced
	// to register hosted services; the production host (via AddWhisperServices) and the host-lifecycle
	// specs opt in explicitly.
	public static IServiceCollection AddAppManagementHostedServices(this IServiceCollection services)
	{
		// The global hotkey listener runs for the app's whole lifetime: start it on launch, stop it on
		// graceful shutdown. Registered as IHostedService so the Generic Host owns its lifecycle.
		services.AddHostedService<HotkeyListenerHostedService>();

		// Hotkey configuration (WHISPER-75): bind the live matcher to the persisted hotkey at startup and
		// rebind it immediately whenever the setting changes, so assigning a hotkey actually takes effect.
		services.AddHostedService<HotkeyConfigurationHostedService>();

		// Settings persistence (WHISPER-43): load the persisted settings into the holder on startup and
		// write them back on graceful shutdown, around the host lifetime.
		services.AddHostedService<SettingsLifecycleService>();

		// Dictation orchestration (WHISPER-14): activate the end-to-end pipeline for the app lifetime —
		// open one long-lived scope, resolve the scoped orchestrator, and bridge the hotkey listener into
		// the activation controller so a real key press drives capture -> transcribe -> deliver.
		services.AddHostedService<DictationOrchestratorHostedService>();

		// Auto-update check (WHISPER-29): on launch, check the release channel in the background (when the
		// user has opted in). Production-only — the specs drive the update policy directly.
		services.AddHostedService<AutoUpdateHostedService>();

		return services;
	}
}
