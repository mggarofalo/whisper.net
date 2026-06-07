// Per-layer DI registration for Logic.AudioManagement. This is the composition seam the Generic Host
// and the BDD specs call; concrete audio behaviors (silence trimming, filler-word cleanup) are
// registered here as they are added (WHISPER-58 onward).

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Logic.AudioManagement.DependencyInjection;

public static class AudioManagementServiceCollectionExtensions
{
	public static IServiceCollection AddAudioManagement(this IServiceCollection services, IConfiguration? configuration = null) =>
		services;
}
