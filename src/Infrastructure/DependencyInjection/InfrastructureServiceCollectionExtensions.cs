// Per-layer DI registration for Infrastructure — the adapters that implement Application ports
// (Whisper.net, NAudio, ONNX VAD, SendInput, persistence). This is the composition seam the Generic
// Host calls; the BDD specs deliberately do NOT call it, substituting the ports with fakes instead.
// Concrete adapters are registered here as later modules add them.

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Infrastructure.DependencyInjection;

public static class InfrastructureServiceCollectionExtensions
{
	public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration? configuration = null) =>
		services;
}
