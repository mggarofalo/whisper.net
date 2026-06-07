// Per-layer DI registration for Logic.AudioManagement. This is the composition seam the Generic Host
// and the BDD specs call; it registers the real audio behaviors so specs exercise them for real
// (only Infrastructure ports are faked).

using Application.Ports;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Logic.AudioManagement.DependencyInjection;

public static class AudioManagementServiceCollectionExtensions
{
	public static IServiceCollection AddAudioManagement(this IServiceCollection services, IConfiguration? configuration = null)
	{
		services.AddSingleton<ISilenceTrimmer, SilenceTrimmer>();
		services.AddSingleton<IFillerWordCleaner, FillerWordCleaner>();

		// Capture normalization (WHISPER-23): the resampler is a stateless, deterministic behavior. The
		// stateful CaptureBuffer is constructed by the orchestration layer (Module 7) with the per-app
		// buffering options, so it is not registered here.
		services.AddSingleton<AudioResampler>();

		// VAD silence policy (WHISPER-31): the deterministic gate/trim behavior over VAD probabilities.
		services.AddSingleton<VadSilencePolicy>();

		// Device-selection policy (WHISPER-13): resolves the stored selection against available devices.
		services.AddSingleton<DeviceSelectionPolicy>();

		return services;
	}
}
