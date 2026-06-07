// Per-layer DI registration for Infrastructure — the adapters that implement Application ports
// (Whisper.net, NAudio, ONNX VAD, SendInput, persistence). This is the composition seam the Generic
// Host calls; the BDD specs deliberately do NOT call it, substituting the ports with fakes instead.
// Concrete adapters are registered here as later modules add them.

using Application.Delivery;
using Application.Ports;
using Infrastructure.Audio;
using Infrastructure.Gpu;
using Infrastructure.Models;
using Infrastructure.TextDelivery;
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

		// Model lifecycle runtime (WHISPER-15): the native load/warmup/transcribe/release operations the
		// lifecycle policy (Logic.ModelManagement) drives, built on the Whisper.net engine seam above.
		services.AddSingleton<IModelRuntime, WhisperModelRuntime>();

		// Model registry cache + download (WHISPER-4). Cache detection is filesystem-only (no network).
		// The downloader fetches a missing model from Hugging Face — the one model-related egress — and
		// verifies it before moving it into the cache. No background fetch is wired: a download happens
		// only when explicitly requested. The cache directory defaults to a per-user folder when unset.
		services.AddOptions<ModelCacheOptions>();
		if (configuration is not null)
		{
			services.Configure<ModelCacheOptions>(configuration.GetSection(ModelCacheOptions.SectionName));
		}

		services.PostConfigure<ModelCacheOptions>(cacheOptions =>
		{
			if (string.IsNullOrWhiteSpace(cacheOptions.CacheDirectory))
			{
				cacheOptions.CacheDirectory = Path.Combine(
					Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
					"whisper.net",
					"models");
			}
		});

		services.AddSingleton<IModelCache, FileSystemModelCache>();
		services.AddSingleton<IModelDownloadSource>(_ => new HuggingFaceModelDownloadSource(new HttpClient()));
		services.AddSingleton<IModelDownloader, ModelDownloader>();

		// Text delivery: the two strategies and the factory that routes between them (WHISPER-2/5/8).
		// SendInputTextInjector types Unicode keystrokes over the Win32 SendInput seam (the universal path
		// that lands even in terminals that ignore paste); ClipboardTextInjector writes text, issues Ctrl+V,
		// and restores the prior clipboard unless a concurrent copy advanced the change count. Both are
		// registered as concrete types and selected per delivery by TextInjectorFactory. Resolving any of
		// them performs no I/O; they act only when Inject is called.
		services.AddSingleton<IKeyboardInput, Win32KeyboardInput>();
		services.AddSingleton<IClipboard, Win32Clipboard>();
		services.AddSingleton<SendInputTextInjector>();
		services.AddSingleton<ClipboardTextInjector>();
		services.AddSingleton<ITextInjectorFactory, TextInjectorFactory>();

		// UIPI / elevation detection (WHISPER-6): lets the delivery pipeline detect a higher-integrity
		// foreground window and surface that synthetic input would be dropped, instead of failing silently.
		services.AddSingleton<IForegroundIntegrityProbe, Win32ForegroundIntegrityProbe>();

		return services;
	}
}
