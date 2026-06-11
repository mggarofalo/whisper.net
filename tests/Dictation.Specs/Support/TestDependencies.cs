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
		services.AddScoped(_ => Substitute.For<IWhisperRuntimeProbe>());

		// The settings-store substitute honors the port contract out of the box: LoadAsync returns the
		// defaults (never null), exactly as a fresh install would. Activating the hotkey section triggers a
		// load (WHISPER-109), so scenarios that merely navigate the shell — without configuring the store —
		// must still compose a contract-respecting load. Drivers that need specific persisted state
		// re-configure LoadAsync, which overrides this default.
		services.AddScoped(_ =>
		{
			ISettingsStore store = Substitute.For<ISettingsStore>();
			store.LoadAsync(Arg.Any<CancellationToken>()).Returns(Domain.Settings.AppSettings.Default);
			return store;
		});
		// The history-store substitute honors the port contract out of the box: an empty history (never a
		// null list), exactly as a fresh install would. Opening the History or Stats section triggers a
		// load on first activation (WHISPER-108), so scenarios that merely navigate the shell — without
		// configuring the store — must still compose a contract-respecting read. Drivers that need entries
		// re-configure GetEntriesAsync, which overrides this default.
		services.AddScoped(_ =>
		{
			IHistoryStore store = Substitute.For<IHistoryStore>();
			store.GetEntriesAsync(Arg.Any<DateTimeOffset?>(), Arg.Any<DateTimeOffset?>(), Arg.Any<int?>(), Arg.Any<CancellationToken>())
				.Returns(Array.Empty<Domain.History.TranscriptEntry>());
			return store;
		});
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

		// Capture tail after chord release (WHISPER-112): a scenario-scoped manual clock overrides the
		// TimeProvider.System registered by AddApplication, so the orchestrator's post-release grace
		// window elapses under test control instead of on the wall clock — drivers that run a stop drain
		// it via StopAndElapseGraceAsync. The driver runs the fake device in deferred-stop mode (NAudio's
		// real timing) against the real orchestrator + delivery pipeline and the real SilenceTrimmer.
		services.AddScoped<ManualTimeProvider>();
		services.AddScoped<TimeProvider>(sp => sp.GetRequiredService<ManualTimeProvider>());
		services.AddScoped<CaptureTailDriver>();

		// Long dictation soft limit (WHISPER-111): a scenario-scoped, mutable holder behind the
		// AudioBufferingOptions resolution (overriding the production singleton), so the long-dictation
		// scenarios can shrink the soft limit BEFORE the orchestrator — which captures the options at
		// construction — is first resolved. Scenarios that never touch the holder get the production
		// defaults, exactly as the singleton registration provided.
		services.AddScoped<ScenarioAudioBufferingOptions>();
		services.AddScoped(sp => sp.GetRequiredService<ScenarioAudioBufferingOptions>().Options);
		services.AddScoped<LongDictationDriver>();

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

		// UI dispatcher seam (WHISPER-90): the driver builds the real tray/overlay view-models over their
		// real controllers and a synchronous recording dispatcher, so the marshaling contract (post vs
		// CheckAccess fast-path, never blocking) is proven with no live WPF Application.
		services.AddScoped<UiDispatcherDriver>();

		// Model picker ports (WHISPER-27): the catalog + model lifecycle are the real ones from
		// AddModelManagement, but the device-facing seams — the filesystem cache, the network downloader,
		// and the lifecycle (whose runtime is not composed in specs) — are substituted so the picker's
		// list/download/switch flow runs over fakes. The IModelLifecycle scoped substitute overrides the
		// real singleton from AddModelManagement for the scenarios that resolve it.
		services.AddScoped(_ => Substitute.For<IModelCache>());
		services.AddScoped(_ => Substitute.For<IModelDownloader>());

		// Like the settings store above, the lifecycle substitute honors its contract out of the box:
		// Status is never null (no model loaded). Opening the Model section triggers a load on first
		// activation (WHISPER-108), so the ListModels handler must read a contract-respecting status even
		// in scenarios that merely navigate the shell. Drivers re-configure Status as needed.
		services.AddScoped(_ =>
		{
			IModelLifecycle lifecycle = Substitute.For<IModelLifecycle>();
			lifecycle.Status.Returns(Domain.Models.ModelStatus.Unloaded);
			return lifecycle;
		});

		// MVVM shell navigation (WHISPER-19): the real ShellViewModel + NavigationService + feature
		// view-models (registered by AddAppManagement) resolved from the scenario scope, so navigation and
		// the Model section's Mediator round-trip run for real over the faked model lifecycle.
		services.AddScoped<ShellNavigationDriver>();

		// Settings/feature view-model foundation (WHISPER-76): resolve the real feature section view-models
		// from the scope and assert they share the validation-capable ObservableValidator base and raise
		// source-generated change notification — the foundation the rest of M12 builds on.
		services.AddScoped<SettingsViewModelFoundationDriver>();

		// Native settings validation (WHISPER-77): the real HotkeyViewModel over the real Mediator pipeline
		// and faked settings store, so the DataAnnotations + INotifyDataErrorInfo gate (invalid chord flags a
		// field error and blocks the save; valid chord persists) is proven at the view-model boundary.
		services.AddScoped<SettingsValidationDriver>();

		// Instant-apply channel (WHISPER-78): the messenger + channel are singletons in production, but the
		// specs share one root provider, so a singleton WeakReferenceMessenger would leak recipient
		// registrations across scenarios. Scope both per scenario so each gets a fresh instant-apply channel.
		services.AddScoped<CommunityToolkit.Mvvm.Messaging.IMessenger, CommunityToolkit.Mvvm.Messaging.WeakReferenceMessenger>();
		services.AddScoped<Application.Settings.SettingsChangeChannel>();

		// Hotkey capture (WHISPER-79): the WPF-free capture interpreter feeding the validated HotkeyViewModel
		// over the real Mediator pipeline and faked store, so the capture rules + validation + persistence gate
		// are proven without WPF (the capture control itself is Presentation glue verified by smoke).
		services.AddScoped<HotkeyCaptureDriver>();

		// Audio-device picker (WHISPER-80): the real AudioDeviceViewModel over the real Mediator pipeline and
		// the faked device enumerator + settings store, so the friendly-name listing, commit, and the
		// removed-device fallback/warning are proven WPF-free (the ComboBox view is smoke-only).
		services.AddScoped<AudioDevicePickerDriver>();

		// Model download (WHISPER-81): the real ModelViewModel over the real Mediator pipeline + catalog with
		// the downloader gated, so the in-flight/cancel/native-error outcomes are proven WPF-free (the
		// ProgressBar + Cancel button are smoke-only).
		services.AddScoped<ModelDownloadDriver>();

		// Concurrent model downloads (WHISPER-107): the real ModelViewModel over the real Mediator pipeline
		// + catalog with the downloader gated PER MODEL ID, so two downloads can be observed in flight at
		// once and one can be cancelled without affecting the other — proving per-row download ownership.
		services.AddScoped<ConcurrentModelDownloadDriver>();

		// Compact model table (WHISPER-105): the real ModelViewModel over the real Mediator pipeline +
		// catalog with the cache/lifecycle faked, so each row's contextual action (Download / Cancel /
		// Select) is asserted from genuine downloaded/active/downloading state.
		services.AddScoped<ModelRowActionsDriver>();

		// First-run setup decision (WHISPER-82): the real GetSetupStatusQuery + SwitchActiveModel over the
		// real Mediator pipeline and catalog, faking the store + cache, so the launch decision (open settings
		// when unconfigured; mark done when a model is activated) is proven WPF-free. The App.xaml wiring that
		// calls IShellPresenter.ShowSettings on that decision is Presentation glue verified by smoke.
		services.AddScoped<SetupStatusDriver>();

		// Accessibility (WHISPER-83): inspects the settings view XAML directly (like the packaging/guidance
		// drivers) to assert automation names, the labelled pickers, the capture control's announced binding,
		// and a declared keyboard tab order. Screen-reader announcement of errors is verified manually.
		services.AddScoped<AccessibilityDriver>();

		// Native theming (WHISPER-84): inspects the presentation artifacts to assert the built-in Fluent
		// ThemeMode.System opt-in and the recorded theming decision. The themed window is smoke-only.
		services.AddScoped<ThemingDriver>();

		// Sidebar contrast (WHISPER-103): inspects the presentation artifacts to compute WCAG AA contrast
		// from the actual nav brush colours and assert the sidebar uses shared brushes (no view-local hex).
		// The live rendered states are smoke + manual.
		services.AddScoped<SidebarThemeDriver>();

		// View resolution convention (WHISPER-92): inspects the documented standard, the shell's implicit
		// DataTemplates (against the real registered sections), and the code-behind discipline; the commit
		// decision that moved out of the device view's code-behind is driven via the picker driver.
		services.AddScoped<ViewResolutionDriver>();

		// Cross-thread collection binding (WHISPER-91): list-bearing view-models register their bound
		// collections through this seam at construction; the recorder lets specs assert the registration
		// (collection + gate) without WPF. Registered for every scenario that resolves such a view-model.
		services.AddScoped<RecordingCollectionSynchronizer>();
		services.AddScoped<IUiCollectionSynchronizer>(sp => sp.GetRequiredService<RecordingCollectionSynchronizer>());
		services.AddScoped<CollectionSyncDriver>();

		// Declarative event wiring (WHISPER-93): inspects the views, the reusable focus behavior, the
		// behaviors package reference, and the committed wiring guideline. Pure-artifact, like theming.
		services.AddScoped<DeclarativeWiringDriver>();

		// VM activation lifecycle (WHISPER-94): real shell navigation over the cached feature view-models
		// and the scenario-scoped messenger, proving subscriptions live exactly while a section is active.
		services.AddScoped<VmLifecycleDriver>();

		// Section auto-load on first activation (WHISPER-108): real shell navigation over the cached
		// feature view-models, proving each data section populates itself when first opened, that rapid
		// tab switching never re-queries, and that an in-flight load reports it cannot execute again.
		services.AddScoped<SectionAutoLoadDriver>();

		// Error surfacing (WHISPER-95): the recorder overrides the production IUserNotifier mapping so the
		// failing-pipeline scenarios assert a notification was requested; the driver also exercises the
		// real TrayUserNotifier directly over a recording dispatcher for the marshal/degrade contract.
		services.AddScoped<RecordingUserNotifier>();
		services.AddScoped<IUserNotifier>(sp => sp.GetRequiredService<RecordingUserNotifier>());
		services.AddScoped<UserNotificationDriver>();

		// View smoke harness (WHISPER-96): inspects the STA smoke project, the binding-error gate, the
		// template completeness check, the FlaUI decision, and the CI gate. The smoke tests themselves
		// are WPF and run in their own project within the same fast gate.
		services.AddScoped<ViewSmokeDriver>();

		// Model picker (WHISPER-27): the real ModelViewModel over the real Mediator pipeline (list /
		// download / switch handlers) and the real catalog, faking only the device-facing model ports.
		services.AddScoped<ModelPickerDriver>();

		// History browser (WHISPER-45): the real HistoryViewModel over the real Mediator pipeline
		// (BrowseHistory + CopyToClipboard) and the faked IHistoryStore + IClipboard. The clipboard
		// substitute backs the re-copy command (no real clipboard is touched in specs).
		services.AddScoped(_ => Substitute.For<IClipboard>());
		services.AddScoped<HistoryBrowserDriver>();

		// History write-through (WHISPER-110): re-configures this scenario's IHistoryStore substitute to
		// round-trip (AddAsync keeps entries; GetEntriesAsync returns them), so a dictation run through the
		// real orchestrator is asserted via the real read path (history browser / stats dashboard) above.
		services.AddScoped<HistoryRecordingDriver>();

		// Stats dashboard (WHISPER-53): the real StatsViewModel over the real Mediator pipeline
		// (GetUsageStats + the real Logic usage-stats calculator) and the faked IHistoryStore, so the
		// dashboard's totals are genuinely computed by the Application layer.
		services.AddScoped<StatsDashboardDriver>();

		// Home status dashboard (WHISPER-106): the real HomeViewModel over the real Mediator pipeline
		// (GetSettings / ListCaptureDevices / GetUsageStats / BrowseHistory) and the faked store/enumerator,
		// so the landing dashboard surfaces genuinely live status from settings and history.
		services.AddScoped<HomeDashboardDriver>();

		// Input-permission probe: substituted so the diagnostics hotkey check can be driven (the onboarding
		// flow that also used it was removed in WHISPER-82; settings is now the single first-run surface).
		services.AddScoped(_ => Substitute.For<IPermissionProbe>());

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

		// Diagnosable logging (WHISPER-73): drives the real AddSerilogLogging composition pointed at a temp
		// directory and asserts the event lands in a rolling log file on disk.
		services.AddScoped<LoggingDriver>();

		// App-data location (WHISPER-86): drives the real AddInfrastructure composition to resolve the model
		// cache + settings DB defaults (plus the logs path) and asserts none collide with the Velopack
		// install root, so installing/updating over a running app never touches or locks user data.
		services.AddScoped<AppDataLocationDriver>();

		// Hotkey reassignment (WHISPER-75): drives the real HotkeyConfigurationHostedService + activation
		// controller over the Mediator pipeline, proving startup config and live rebind on a settings change.
		services.AddScoped<HotkeyConfigurationDriver>();

		// Hotkey assignment end-to-end (WHISPER-109): the real HotkeyViewModel entered through the REAL
		// navigation lifecycle (OnNavigatedTo) over the real Mediator pipeline + hosted service + activation
		// controller, faking only the round-trip store — proving activation seeds the section and an
		// assignment applies live and on the next launch. The driver owns its controller/service instances
		// (a relaunch needs fresh ones), so only the driver itself is registered.
		services.AddScoped<HotkeyAssignmentDriver>();

		// Signed auto-update (WHISPER-29): the real AutoUpdateService policy over a faked update source, so
		// the check/download/apply, opt-in gating, and graceful-degradation behaviour run without Velopack
		// or network. The driver builds the service itself, so only the faked source needs registering.
		services.AddScoped(_ => Substitute.For<IUpdateSource>());
		services.AddScoped<AutoUpdateDriver>();

		return services;
	}
}
