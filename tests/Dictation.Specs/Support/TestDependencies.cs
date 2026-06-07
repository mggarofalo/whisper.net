// Wires the BDD scenarios to the application's REAL composition. The Reqnroll DI plugin calls this
// per scenario, builds a fresh scope, and resolves the [Binding] step classes (and the driver) from
// it. Crucially this calls the SAME per-layer registration extensions the production host uses, so
// the specs exercise production composition — only the Infrastructure ports are substituted.

using Application.DependencyInjection;
using Application.Ports;
using Dictation.Specs.Drivers;
using Infrastructure.Audio;
using Logic.AppManagement.DependencyInjection;
using Logic.AudioManagement.DependencyInjection;
using Logic.GpuContactPoint.DependencyInjection;
using Logic.ModelManagement.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
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
		services.AddScoped(_ => Substitute.For<ITextInjector>());
		services.AddScoped(_ => Substitute.For<ISettingsStore>());
		services.AddScoped(_ => Substitute.For<IHistoryStore>());
		services.AddScoped(_ => Substitute.For<IGpuProbe>());

		// Capture (WHISPER-7): drive the REAL WasapiAudioSource over a fake device seam, so the
		// capture contract is validated for real while no microphone is touched.
		services.AddScoped<FakeAudioCaptureClient>();
		services.AddScoped<IAudioCaptureClient>(sp => sp.GetRequiredService<FakeAudioCaptureClient>());
		services.AddScoped<IAudioSource, WasapiAudioSource>();

		services.AddScoped<ScenarioWorld>();
		services.AddScoped<TranscriptionDriver>();

		// Text delivery (WHISPER-2): the real SendInputTextInjector over a recording fake keyboard seam.
		services.AddScoped<TextInjectionDriver>();
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
