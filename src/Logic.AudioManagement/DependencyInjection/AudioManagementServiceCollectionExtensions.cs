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

		return services;
	}
}
