// Wires the BDD scenarios to the application's REAL composition. The Reqnroll DI plugin calls this
// per scenario, builds a fresh scope, and resolves the [Binding] step classes (and the driver) from
// it. Crucially this calls the SAME per-layer registration extensions the production host uses, so
// the specs exercise production composition — only the Infrastructure ports are substituted.

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

		// Host bootstrapping (WHISPER-12): the driver builds its own real Generic Host internally over a
		// fake hook seam, so it is registered plainly and owns the hosted-service lifecycle it asserts.
		services.AddScoped<AppLifecycleDriver>();

		// Settings persistence (WHISPER-43): the driver composes the real lifecycle service over the real
		// file-backed store against a private temp directory, so it owns its own wiring.
		services.AddScoped<SettingsPersistenceDriver>();

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
		services.AddScoped<IPostProcessor, Logic.AppManagement.PostProcessing.PostProcessPipeline>();
		services.AddScoped<PostProcessPipelineDriver>();

		// GPU contact point (WHISPER-9): the real backend selector over a faked raw probe.
		services.AddScoped<GpuBackendDriver>();

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

		return services;
	}
}
