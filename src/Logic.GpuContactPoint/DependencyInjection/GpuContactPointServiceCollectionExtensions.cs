// Per-layer DI registration for Logic.GpuContactPoint. This is the composition seam the Generic Host
// and the BDD specs call; the GPU-vs-CPU runtime policy services are registered here once Module 3
// lands (see the WHISPER-65 spike).

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Logic.GpuContactPoint.DependencyInjection;

public static class GpuContactPointServiceCollectionExtensions
{
	public static IServiceCollection AddGpuContactPoint(this IServiceCollection services, IConfiguration? configuration = null) =>
		services;
}
