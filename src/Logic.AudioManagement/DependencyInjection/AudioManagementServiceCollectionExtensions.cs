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
		// Trailing-silence trim tunables: a plain default today, like AudioBufferingOptions;
		// bound from configuration later if the thresholds ever need to be user-tunable.
		services.AddSingleton(new SilenceTrimmerOptions());
		services.AddSingleton<ISilenceTrimmer, SilenceTrimmer>();
		services.AddSingleton<IFillerWordCleaner, FillerWordCleaner>();

		// Capture normalization: the resampler is a stateless, deterministic behavior. The
		// stateful CaptureBuffer is constructed by the orchestration layer (Module 7) with the per-app
		// buffering options, so it is not registered here.
		services.AddSingleton<AudioResampler>();

		// VAD silence policy: the deterministic gate/trim behavior over VAD probabilities.
		services.AddSingleton<VadSilencePolicy>();

		// Device-selection policy: resolves the stored selection against available devices.
		services.AddSingleton<DeviceSelectionPolicy>();

		return services;
	}
}
