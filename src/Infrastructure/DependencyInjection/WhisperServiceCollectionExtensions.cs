// Full production composition: registers every layer through its own per-layer registration
// extension. The Generic Host calls this for the complete wiring. The BDD specs deliberately do NOT
// call this — they compose only the inner layers (Application + Logic.*) and substitute the
// Infrastructure ports with fakes — which is exactly why each layer owns its own AddX seam.

using Application.DependencyInjection;
using Logic.AppManagement.DependencyInjection;
using Logic.AudioManagement.DependencyInjection;
using Logic.GpuContactPoint.DependencyInjection;
using Logic.ModelManagement.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Infrastructure.DependencyInjection;

public static class WhisperServiceCollectionExtensions
{
	public static IServiceCollection AddWhisperServices(this IServiceCollection services, IConfiguration configuration)
	{
		services.AddApplication(configuration);
		services.AddAppManagement(configuration);
		services.AddAudioManagement(configuration);
		services.AddModelManagement(configuration);
		services.AddGpuContactPoint(configuration);
		services.AddInfrastructure(configuration);

		// The host-owned background components: start the long-lived hotkey listener as a
		// hosted service so the Generic Host owns its lifetime. Only the full production composition wires
		// these — the specs drive them through their own host, so this stays out of the scenario container.
		services.AddAppManagementHostedServices();

		return services;
	}
}
