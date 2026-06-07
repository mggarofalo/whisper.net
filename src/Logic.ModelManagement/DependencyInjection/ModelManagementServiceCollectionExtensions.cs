// Per-layer DI registration for Logic.ModelManagement. This is the composition seam the Generic Host
// and the BDD specs call; it registers the on-device model registry so the rest of the app can list
// models and resolve ids (the cache and downloader live in Infrastructure).

using Application.Ports;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Logic.ModelManagement.DependencyInjection;

public static class ModelManagementServiceCollectionExtensions
{
	public static IServiceCollection AddModelManagement(this IServiceCollection services, IConfiguration? configuration = null)
	{
		// Model registry (WHISPER-4): the static catalog of supported Whisper variants. Pure data.
		services.AddSingleton<IModelCatalog, WhisperModelCatalog>();

		return services;
	}
}
