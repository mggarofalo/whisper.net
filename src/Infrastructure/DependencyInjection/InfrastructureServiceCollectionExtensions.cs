// Per-layer DI registration for Infrastructure — the adapters that implement Application ports
// (Whisper.net, NAudio, ONNX VAD, SendInput, persistence). This is the composition seam the Generic
// Host calls; the BDD specs deliberately do NOT call it, substituting the ports with fakes instead.
// Concrete adapters are registered here as later modules add them.

using Application.Ports;
using Infrastructure.Audio;
using Infrastructure.Gpu;
using Infrastructure.Transcription;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Infrastructure.DependencyInjection;

public static class InfrastructureServiceCollectionExtensions
{
	public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration? configuration = null)
	{
		// Microphone capture (WHISPER-7): the WASAPI source over the real NAudio device client.
		services.AddSingleton<IAudioCaptureClient, NAudioCaptureClient>();
		services.AddSingleton<IAudioSource, WasapiAudioSource>();

		// Voice-activity detection (WHISPER-31): the Silero adapter over the on-device ONNX session.
		// The session loads its model lazily, so resolving the port never requires the asset present.
		services.AddSingleton<IVadSession>(_ => new OnnxVadSession(
			Path.Combine(AppContext.BaseDirectory, "assets", "silero_vad.onnx")));
		services.AddSingleton<IVad, SileroVad>();

		// Device enumeration + default-change notification (WHISPER-13). Both create their underlying
		// NAudio enumerator lazily, so resolving the ports touches no audio hardware.
		services.AddSingleton<IAudioDeviceEnumerator, NAudioDeviceEnumerator>();
		services.AddSingleton<IDefaultDeviceWatcher, NAudioDefaultDeviceWatcher>();

		// GPU runtime detection (WHISPER-9): the raw Vulkan-loader probe the GPU contact point consults.
		// It resolves the loader without initializing a device, so it returns promptly and never hangs.
		services.AddSingleton<IGpuProbe, VulkanGpuProbe>();

		// Transcription (WHISPER-3): the Whisper.net adapter over an internal engine seam. The model is
		// loaded lazily on first transcription, so resolving the port touches no model file or native
		// library; the model path/language come from the bound WhisperOptions.
		services.AddOptions<WhisperOptions>();
		if (configuration is not null)
		{
			services.Configure<WhisperOptions>(configuration.GetSection(WhisperOptions.SectionName));
		}

		services.AddSingleton<IWhisperEngineFactory, WhisperNetEngineFactory>();
		services.AddSingleton<ITranscriber, WhisperTranscriber>();

		return services;
	}
}
