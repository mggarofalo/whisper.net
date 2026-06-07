// Per-layer DI registration for Logic.AppManagement. This is the composition seam the Generic Host
// and the BDD specs call; concrete app-management behaviors are registered here as later modules add
// them.

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Logic.AppManagement.DependencyInjection;

public static class AppManagementServiceCollectionExtensions
{
	public static IServiceCollection AddAppManagement(this IServiceCollection services, IConfiguration? configuration = null) =>
		services;
}
