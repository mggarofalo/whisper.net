// Per-layer DI registration for Infrastructure — the adapters that implement Application ports
// (Whisper.net, NAudio, ONNX VAD, SendInput, persistence). This is the composition seam the Generic
// Host calls; the BDD specs deliberately do NOT call it, substituting the ports with fakes instead.
// Concrete adapters are registered here as later modules add them.

using Application.Ports;
using Infrastructure.Audio;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Infrastructure.DependencyInjection;

public static class InfrastructureServiceCollectionExtensions
{
	public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration? configuration = null)
	{
		// Microphone capture (WHISPER-7): the WASAPI source over the real NAudio device client.
		services.AddSingleton<IAudioCaptureClient, NAudioCaptureClient>();
		services.AddSingleton<IAudioSource, WasapiAudioSource>();

		// Voice-activity detection (WHISPER-31): the Silero adapter over the on-device ONNX session.
		// The session loads its model lazily, so resolving the port never requires the asset present.
		services.AddSingleton<IVadSession>(_ => new OnnxVadSession(
			Path.Combine(AppContext.BaseDirectory, "assets", "silero_vad.onnx")));
		services.AddSingleton<IVad, SileroVad>();

		return services;
	}
}
