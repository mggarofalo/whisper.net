// Wires the BDD scenarios to the application's REAL composition. The Reqnroll DI plugin calls this
// per scenario, builds a fresh scope, and resolves the [Binding] step classes (and the driver) from
// it. Crucially this calls the SAME per-layer registration extensions the production host uses, so
// the specs exercise production composition — only the Infrastructure ports are substituted.

using Application.Commands;
using Application.Configuration;
using Application.Delivery;
using Application.DependencyInjection;
using Application.Ports;
using Dictation.Specs.Drivers;
using Infrastructure.Audio;
using Infrastructure.Hotkeys;
using Logic.AppManagement;
using Logic.AppManagement.DependencyInjection;
using Logic.AppManagement.OutputTransforms;
using Logic.AudioManagement.DependencyInjection;
using Logic.GpuContactPoint.DependencyInjection;
using Logic.ModelManagement.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using Reqnroll.Microsoft.Extensions.DependencyInjection;

namespace Dictation.Specs.Support;

public static class TestDependencies
{
	[ScenarioDependencies]
	public static IServiceCollection CreateServices()
	{
		ServiceCollection services = new();

		// Logging infrastructure with no providers: ILogger<T> resolves to a no-op logger, so adapters
		// that log (e.g. the hotkey listener) compose for real without Serilog's console sink in specs.
		services.AddLogging();

		// Real production registration — the inner layers run for real.
		services.AddApplication();
		services.AddAppManagement();
		services.AddAudioManagement();
		services.AddModelManagement();
		services.AddGpuContactPoint();

		// Substitute ONLY the Infrastructure ports — the seams the specs control.
		services.AddScoped(_ => Substitute.For<ITranscriber>());
		services.AddScoped(_ => Substitute.For<ISettingsStore>());
		services.AddScoped(_ => Substitute.For<IHistoryStore>());
		services.AddScoped(_ => Substitute.For<IAuditLog>());
		services.AddScoped(_ => Substitute.For<IGpuProbe>());

		// Foreground integrity (WHISPER-6): default substitute returns Same (the enum's default), so the
		// existing delivery specs type normally; the UIPI specs override it to a higher-integrity window.
		services.AddScoped(_ => Substitute.For<IForegroundIntegrityProbe>());

		// Delivery routing (WHISPER-8): the fake factory exposes a typing and a paste injector substitute,
		// so specs assert which path the pipeline chose. Replaces the single ITextInjector substitute.
		services.AddScoped<FakeTextInjectorFactory>();
		services.AddScoped<ITextInjectorFactory>(sp => sp.GetRequiredService<FakeTextInjectorFactory>());

		// Delivery options (WHISPER-8): a scenario-scoped, mutable instance the strategy driver sets in a
		// Given, so the configured default can vary per scenario. Overrides the production binding.
		services.AddScoped<DeliveryOptions>();
		services.AddScoped<IOptions<DeliveryOptions>>(sp => Options.Create(sp.GetRequiredService<DeliveryOptions>()));

		// Capture (WHISPER-7): drive the REAL WasapiAudioSource over a fake device seam, so the
		// capture contract is validated for real while no microphone is touched.
		services.AddScoped<FakeAudioCaptureClient>();
		services.AddScoped<IAudioCaptureClient>(sp => sp.GetRequiredService<FakeAudioCaptureClient>());
		services.AddScoped<IAudioSource, WasapiAudioSource>();

		// Global hotkeys (WHISPER-10): drive the REAL EventLoopHotkeyListener over a fake hook seam, so
		// the threading, translation, and modifier tracking are validated for real with no OS hook.
		services.AddScoped<FakeGlobalKeyHook>();
		services.AddScoped<IGlobalKeyHook>(sp => sp.GetRequiredService<FakeGlobalKeyHook>());
		services.AddScoped<IHotkeyListener, EventLoopHotkeyListener>();

		services.AddScoped<ScenarioWorld>();
		services.AddScoped<TranscriptionDriver>();
		services.AddScoped<HotkeyListenerDriver>();

		// End-to-end dictation orchestration (WHISPER-14): the REAL DictationOrchestrator (scoped via
		// AddAppManagement) drives the real WasapiAudioSource over the fake capture client and the real
		// delivery pipeline through Mediator, faking only the Infrastructure ports. A RecordingLogger backs
		// ILogger<DictationOrchestrator> so the failure scenario can assert the structured error log; it is
		// registered after AddLogging so this closed-generic wins over the no-op open-generic logger.
		services.AddScoped<RecordingLogger<DictationOrchestrator>>();
		services.AddScoped<ILogger<DictationOrchestrator>>(sp => sp.GetRequiredService<RecordingLogger<DictationOrchestrator>>());
		services.AddScoped<DictationOrchestratorDriver>();

		// Command-mode hook (WHISPER-35): substitute the matcher (default: no match) so the command-mode
		// driver can make it recognize a command, while every other delivery scenario behaves exactly as
		// before. Overrides the production NoOpCommandMatcher registered by AddAppManagement.
		services.AddScoped(_ =>
		{
			ICommandMatcher matcher = Substitute.For<ICommandMatcher>();
			matcher.Match(Arg.Any<string>()).Returns(CommandMatch.None);
			return matcher;
		});
		services.AddScoped<CommandModeDriver>();

		// Continuous dictation mode (WHISPER-28): the REAL orchestrator over the real audio + delivery
		// composition; the driver enters the mode, runs an utterance, and asserts the auto-restart / Esc exit.
		services.AddScoped<ContinuousDictationDriver>();

		// Audio feedback (WHISPER-21): substitute the feedback port so the driver can assert which cue fired
		// without a real output device, and bind a scoped, mutable options instance (default on) the driver
		// toggles per scenario — overriding the IOptions<AudioFeedbackOptions> registered by AddApplication.
		services.AddScoped(_ => Substitute.For<IAudioFeedback>());
		services.AddScoped<AudioFeedbackOptions>();
		services.AddScoped<IOptions<AudioFeedbackOptions>>(sp => Options.Create(sp.GetRequiredService<AudioFeedbackOptions>()));
		services.AddScoped<AudioFeedbackDriver>();

		// Level overlay (WHISPER-26): the driver builds the real LevelOverlayController (the WPF-free
		// view-model logic) over a real state machine and a faked audio source, so it owns its own wiring.
		services.AddScoped<LevelOverlayDriver>();

		// Model picker ports (WHISPER-27): the catalog + model lifecycle are the real ones from
		// AddModelManagement, but the device-facing seams — the filesystem cache, the network downloader,
		// and the lifecycle (whose runtime is not composed in specs) — are substituted so the picker's
		// list/download/switch flow runs over fakes. The IModelLifecycle scoped substitute overrides the
		// real singleton from AddModelManagement for the scenarios that resolve it.
		services.AddScoped(_ => Substitute.For<IModelCache>());
		services.AddScoped(_ => Substitute.For<IModelDownloader>());
		services.AddScoped(_ => Substitute.For<IModelLifecycle>());

		// MVVM shell navigation (WHISPER-19): the real ShellViewModel + NavigationService + feature
		// view-models (registered by AddAppManagement) resolved from the scenario scope, so navigation and
		// the Model section's Mediator round-trip run for real over the faked model lifecycle.
		services.AddScoped<ShellNavigationDriver>();

		// Model picker (WHISPER-27): the real ModelViewModel over the real Mediator pipeline (list /
		// download / switch handlers) and the real catalog, faking only the device-facing model ports.
		services.AddScoped<ModelPickerDriver>();

		// History browser (WHISPER-45): the real HistoryViewModel over the real Mediator pipeline
		// (BrowseHistory + CopyToClipboard) and the faked IHistoryStore + IClipboard. The clipboard
		// substitute backs the re-copy command (no real clipboard is touched in specs).
		services.AddScoped(_ => Substitute.For<IClipboard>());
		services.AddScoped<HistoryBrowserDriver>();

		// Stats dashboard (WHISPER-53): the real StatsViewModel over the real Mediator pipeline
		// (GetUsageStats + the real Logic usage-stats calculator) and the faked IHistoryStore, so the
		// dashboard's totals are genuinely computed by the Application layer.
		services.AddScoped<StatsDashboardDriver>();

		// First-run onboarding (WHISPER-51): the real OnboardingViewModel over the real Mediator pipeline
		// (GetSettings/UpdateSettings/SwitchActiveModel/DownloadModel/CompleteOnboarding) and faked ports —
		// the settings store (round-tripped so completion is remembered), the model downloader, and the
		// permission probe (substituted so the deny-then-grant re-attempt can be driven).
		services.AddScoped(_ => Substitute.For<IPermissionProbe>());
		services.AddScoped<OnboardingDriver>();

		// Host bootstrapping (WHISPER-12): the driver builds its own real Generic Host internally over a
		// fake hook seam, so it is registered plainly and owns the hosted-service lifecycle it asserts.
		services.AddScoped<AppLifecycleDriver>();

		// Settings persistence (WHISPER-43): the driver composes the real lifecycle service over the real
		// SQLite-backed store against a private temp database, so it owns its own wiring.
		services.AddScoped<SettingsPersistenceDriver>();

		// SQLite persistence store (WHISPER-11): the driver exercises the real migration runner + SQLite
		// store directly against a private temp-file database — persistence is the seam under test here.
		services.AddScoped<PersistenceDriver>();

		// History retention + paged browsing (WHISPER-17): the driver builds its own real Mediator pipeline
		// over the real SQLite store against a private temp database, so it owns its own wiring.
		services.AddScoped<HistoryRetentionDriver>();

		// Usage stats recording + aggregation (WHISPER-24): the driver builds its own real Mediator pipeline
		// and Logic aggregator over the real SQLite store against a private temp database, so a restart truly
		// reloads persisted measures from disk.
		services.AddScoped<UsageStatsRecordingDriver>();

		// Opt-in audit log (WHISPER-34): the driver builds its own composition — the real AuditLogger gate +
		// real SQLite history/audit stores against a private temp database — so the default-off, opt-in,
		// hot-toggle, and purge behaviours are proven end to end against the local store.
		services.AddScoped<AuditLogDriver>();

		// Run on login (WHISPER-32): drive the real command/query handlers through IMediator over an
		// in-memory startup registration, the single OS seam this feature controls.
		services.AddScoped<FakeStartupRegistration>();
		services.AddScoped<IStartupRegistration>(sp => sp.GetRequiredService<FakeStartupRegistration>());
		services.AddScoped<RunOnLoginDriver>();

		// Tray icon + menu (WHISPER-18): drive the real TrayController over the real recording state
		// machine, with the shell-presenter and host-lifetime seams faked. The driver builds the
		// controller itself, so only the fakes need registering.
		services.AddScoped<FakeShellPresenter>();
		services.AddScoped<IShellPresenter>(sp => sp.GetRequiredService<FakeShellPresenter>());
		services.AddScoped<FakeApplicationLifetime>();
		services.AddScoped<TrayDriver>();

		// Single-instance enforcement (WHISPER-25): drive the real coordinator over fake lock + signal
		// seams (the driver owns those) and the shared fake shell presenter reused from the tray specs.
		services.AddScoped<SingleInstanceDriver>();

		// Hotkey activation modes (WHISPER-16): the real HotkeyActivationController behind the driver.
		// Override its production singleton lifetime to scoped so each scenario gets fresh chord state.
		services.AddScoped<HotkeyActivationController>();
		services.AddScoped<HotkeyActivationDriver>();

		// Recording state machine (WHISPER-22): the real machine behind the driver, scoped so each
		// scenario starts from a fresh Idle state.
		services.AddScoped<RecordingStateMachine>();
		services.AddScoped<RecordingStateMachineDriver>();

		// Hotkey rebinding (WHISPER-30): the real capture service + controller behind the driver, scoped
		// so each scenario starts from the default binding.
		services.AddScoped<HotkeyCaptureService>();
		services.AddScoped<HotkeyRebindingDriver>();

		// Text delivery (WHISPER-2): the real SendInputTextInjector over a recording fake keyboard seam.
		services.AddScoped<TextInjectionDriver>();

		// Clipboard fallback (WHISPER-5): the real ClipboardTextInjector over fake clipboard + keyboard seams.
		services.AddScoped<ClipboardDeliveryDriver>();

		// UIPI-aware delivery (WHISPER-6): the real pipeline through IMediator with the integrity probe faked.
		services.AddScoped<UipiDeliveryDriver>();

		// Delivery-strategy selection (WHISPER-8): the real pipeline + selector, routing to the fake factory.
		services.AddScoped<DeliveryStrategyDriver>();
		services.AddScoped<RepositoryGuidanceDriver>();
		services.AddScoped<DomainInvariantsDriver>();
		services.AddScoped<ApplicationPortsDriver>();
		services.AddScoped<SettingsDriver>();
		services.AddScoped<HistoryDriver>();
		services.AddScoped<UsageStatsDriver>();
		services.AddScoped<MappingDriver>();
		services.AddScoped<AudioCaptureDriver>();
		services.AddScoped<AudioNormalizationDriver>();
		services.AddScoped<VadDriver>();

		// Transcription normalization (WHISPER-36): the real IFillerWordCleaner behind the driver.
		services.AddScoped<TranscriptionNormalizationDriver>();

		// Custom vocabulary (WHISPER-38): the real VocabularyConditioner behind the assembly driver, and
		// the real WhisperTranscriber over a capturing fake engine behind the transcription driver.
		services.AddScoped<VocabularyConditioningDriver>();
		services.AddScoped<VocabularyTranscriptionDriver>();

		// Opt-in localhost rephrase (WHISPER-40): the real OllamaRephraseClient + validator over a
		// recording HTTP transport, so the opt-in gate and loopback-only rule are proven without a socket.
		services.AddScoped<RephraseDriver>();

		// Output transforms (WHISPER-37): the real OutputTransformService + registry over a faked rephrase
		// port. Override the production singleton service to scoped so each scenario gets the same fresh
		// IRephraseClient substitute the driver configures (no cross-scenario state, no captive singleton).
		services.AddScoped(_ => Substitute.For<IRephraseClient>());
		services.AddScoped<OutputTransformService>();
		services.AddScoped<OutputTransformDriver>();

		// Post-process pipeline (WHISPER-41): the real pipeline behind IPostProcessor, overridden to scoped
		// so it shares the per-scenario OutputTransformService + IRephraseClient substitute the driver sets.
		// The live settings holder must be scoped for the same reason — otherwise a scenario that configures
		// a default transform leaks it (via the production singleton) into later scenarios, whose unconfigured
		// rephrase substitute then yields a null result inside the transform step.
		services.AddScoped<PostProcessSettingsHolder>();
		services.AddScoped<IPostProcessor, Logic.AppManagement.PostProcessing.PostProcessPipeline>();
		services.AddScoped<PostProcessPipelineDriver>();

		// GPU contact point (WHISPER-9): the real backend selector over a faked raw probe.
		services.AddScoped<GpuBackendDriver>();

		// Re-scope the real backend selector (WHISPER-50): AddGpuContactPoint registers it as a singleton,
		// which would capture a root-scope IGpuProbe and ignore the per-scenario substitute. Scoped here so
		// the diagnostics GPU check sees the same scenario-scoped probe the driver configures.
		services.AddScoped<IBackendSelector, Logic.GpuContactPoint.GpuBackendSelector>();

		// On-device transcription (WHISPER-3): the real Whisper.net adapter over a fake engine seam.
		services.AddScoped<WhisperTranscriptionDriver>();

		// Model registry/cache/download (WHISPER-4): real catalog + cache + downloader, hermetic source.
		services.AddScoped<ModelLibraryDriver>();

		// Model lifecycle (WHISPER-15): the real lifecycle policy over a fake runtime.
		services.AddScoped<ModelLifecycleDriver>();

		// Device selection (WHISPER-13): fake enumerator + notification client behind the driver.
		services.AddScoped<FakeAudioDeviceEnumerator>();
		services.AddScoped<FakeDefaultDeviceWatcher>();
		services.AddScoped<AudioDeviceDriver>();

		// Audio device + hotkey configuration (WHISPER-33): bind the device-enumerator port to the fake so
		// the ListCaptureDevices handler resolves it, and drive the real audio/hotkey settings view-models
		// over the real Mediator pipeline (GetSettings/UpdateSettings + validation) and faked store.
		services.AddScoped<IAudioDeviceEnumerator>(sp => sp.GetRequiredService<FakeAudioDeviceEnumerator>());
		services.AddScoped<AudioHotkeyConfigDriver>();

		// Self-diagnostics (WHISPER-50): the real doctor pipeline — RunDiagnosticsQuery through the
		// Application handler and the Logic checks (incl. the real GpuBackendSelector) — over the faked
		// device-facing ports already registered above (device enumerator, settings store, model cache,
		// permission probe, raw GPU probe). The driver configures those fakes per subsystem.
		services.AddScoped<DiagnosticsDriver>();

		// Velopack packaging configuration (WHISPER-20): inspects repository artifacts (the project file,
		// version policy, packaging script, icon, tool manifest) directly, like the repo-guidance driver.
		services.AddScoped<PackagingDriver>();

		// Tag-driven release workflow (WHISPER-39): inspects .github/workflows/release.yml directly.
		services.AddScoped<ReleaseWorkflowDriver>();

		// Self-signed code signing for personal builds (WHISPER-72): inspects the signing-cert helper
		// script, the build-and-run guide, and the README directly, like the packaging driver.
		services.AddScoped<SelfSignedSigningDriver>();

		// Signed auto-update (WHISPER-29): the real AutoUpdateService policy over a faked update source, so
		// the check/download/apply, opt-in gating, and graceful-degradation behaviour run without Velopack
		// or network. The driver builds the service itself, so only the faked source needs registering.
		services.AddScoped(_ => Substitute.For<IUpdateSource>());
		services.AddScoped<AutoUpdateDriver>();

		return services;
	}
}
