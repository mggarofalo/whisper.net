// Per-layer DI registration for Logic.ModelManagement. This is the composition seam the Generic Host
// and the BDD specs call; concrete model registry / cache-selection behaviors are registered here as
// later modules add them.

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Logic.ModelManagement.DependencyInjection;

public static class ModelManagementServiceCollectionExtensions
{
	public static IServiceCollection AddModelManagement(this IServiceCollection services, IConfiguration? configuration = null) =>
		services;
}
