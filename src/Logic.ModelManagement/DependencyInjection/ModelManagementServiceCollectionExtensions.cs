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
		// Model registry: the static catalog of supported Whisper variants. Pure data.
		services.AddSingleton<IModelCatalog, WhisperModelCatalog>();

		// Custom-vocabulary prompt-token conditioning: a pure, stateless assembler that
		// turns a user vocabulary into decoder conditioning (initial prompt + threshold override).
		services.AddSingleton<VocabularyConditioner>();

		// Model lifecycle: the single owner of the loaded model — load/unload/switch,
		// warmup, precision, and concurrency-safe transcription. Pure policy over IModelRuntime.
		services.AddOptions<ModelLifecycleOptions>();
		if (configuration is not null)
		{
			services.Configure<ModelLifecycleOptions>(configuration.GetSection(ModelLifecycleOptions.SectionName));
		}

		services.AddSingleton<IModelLifecycle, ModelLifecycle>();

		return services;
	}
}
