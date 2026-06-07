// Per-layer DI registration for Logic.AppManagement. This is the composition seam the Generic Host
// and the BDD specs call; it registers the real app-management behaviors so specs exercise them for
// real (only Infrastructure ports are faked).

using Application.Ports;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Logic.AppManagement.DependencyInjection;

public static class AppManagementServiceCollectionExtensions
{
	public static IServiceCollection AddAppManagement(this IServiceCollection services, IConfiguration? configuration = null)
	{
		services.AddSingleton<IUsageStatsCalculator, UsageStatsCalculator>();

		return services;
	}
}
