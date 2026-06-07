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
using Logic.AppManagement.DependencyInjection;
using Logic.AudioManagement.DependencyInjection;
using Logic.GpuContactPoint.DependencyInjection;
using Logic.ModelManagement.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
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

		services.AddScoped<ScenarioWorld>();
		services.AddScoped<TranscriptionDriver>();

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
