// Per-layer DI registration for Logic.GpuContactPoint. This is the composition seam the Generic Host
// and the BDD specs call; it registers the GPU-vs-CPU backend policy so specs exercise the real
// decision (only the raw IGpuProbe is faked).

using Application.Ports;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Logic.GpuContactPoint.DependencyInjection;

public static class GpuContactPointServiceCollectionExtensions
{
	public static IServiceCollection AddGpuContactPoint(this IServiceCollection services, IConfiguration? configuration = null)
	{
		// The single GPU contact point (WHISPER-9): decides Vulkan vs CPU from the raw probe's answer.
		services.AddSingleton<IBackendSelector, GpuBackendSelector>();

		return services;
	}
}
