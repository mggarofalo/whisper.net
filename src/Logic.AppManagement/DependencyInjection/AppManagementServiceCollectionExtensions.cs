// Per-layer DI registration for Logic.AppManagement. This is the composition seam the Generic Host
// and the BDD specs call; it registers the real app-management behaviors so specs exercise them for
// real (only Infrastructure ports are faked).

using Application.Delivery;
using Application.Ports;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Logic.AppManagement.DependencyInjection;

public static class AppManagementServiceCollectionExtensions
{
	public static IServiceCollection AddAppManagement(this IServiceCollection services, IConfiguration? configuration = null)
	{
		services.AddSingleton<IUsageStatsCalculator, UsageStatsCalculator>();

		// Delivery-strategy selection (WHISPER-8): pure override-vs-default precedence for each delivery.
		services.AddSingleton<IDeliveryStrategySelector, DeliveryStrategySelector>();

		// Hotkey activation (WHISPER-16): the stateful chord matcher that turns key edges into recording
		// start/stop requests for push-to-talk and toggle. Singleton so it holds the live chord state the
		// orchestration layer (M7) subscribes to and drives from the hotkey listener.
		services.AddSingleton<HotkeyActivationController>();

		return services;
	}
}
