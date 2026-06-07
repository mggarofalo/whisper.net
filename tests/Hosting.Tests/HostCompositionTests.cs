// Verifies the Generic Host composition (WHISPER-57): a host built from the per-layer registration
// extensions resolves the Mediator that dispatches handlers, and Serilog's minimum level is honored
// from configuration (proving the layered config -> logging path). The host uses the exact same
// AddWhisperServices / AddSerilogLogging extensions the WPF app calls.

using Application.Ports;
using Infrastructure.Audio;
using Infrastructure.DependencyInjection;
using Mediator;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Hosting.Tests;

public sealed class HostCompositionTests
{
	private static IHost BuildHost(params KeyValuePair<string, string?>[] settings)
	{
		HostApplicationBuilder builder = Host.CreateApplicationBuilder();
		builder.Configuration.AddInMemoryCollection(settings);

		builder.Services.AddSerilogLogging(builder.Configuration);
		builder.Services.AddWhisperServices(builder.Configuration);

		return builder.Build();
	}

	[Fact]
	public void Resolves_the_mediator_wired_by_the_per_layer_extensions()
	{
		using IHost host = BuildHost();
		using IServiceScope scope = host.Services.CreateScope();

		Assert.NotNull(scope.ServiceProvider.GetService<IMediator>());
	}

	// WHISPER-7 AC6: the WASAPI capture port is registered by the Infrastructure layer and resolvable
	// from the production composition (constructing it must not require a real device).
	[Fact]
	public void Resolves_the_wasapi_audio_capture_port_from_infrastructure()
	{
		using IHost host = BuildHost();

		IAudioSource source = host.Services.GetRequiredService<IAudioSource>();

		Assert.IsType<WasapiAudioSource>(source);
	}

	// WHISPER-31: the Silero VAD port is registered by Infrastructure and resolvable from the
	// production composition (the ONNX model loads lazily, so resolution needs no asset present).
	[Fact]
	public void Resolves_the_silero_vad_port_from_infrastructure()
	{
		using IHost host = BuildHost();

		IVad vad = host.Services.GetRequiredService<IVad>();

		Assert.IsType<SileroVad>(vad);
	}

	// WHISPER-13 AC6: enumeration and the default-device notification client are wired through the
	// Infrastructure DI extension (both create their NAudio enumerator lazily, so resolution is safe).
	[Fact]
	public void Resolves_the_audio_device_enumeration_and_watcher_from_infrastructure()
	{
		using IHost host = BuildHost();

		Assert.NotNull(host.Services.GetService<IAudioDeviceEnumerator>());
		Assert.NotNull(host.Services.GetService<IDefaultDeviceWatcher>());
	}

	[Fact]
	public void Honors_the_minimum_log_level_from_configuration()
	{
		using IHost host = BuildHost(new KeyValuePair<string, string?>("Serilog:MinimumLevel:Default", "Warning"));

		ILogger<HostCompositionTests> logger = host.Services.GetRequiredService<ILogger<HostCompositionTests>>();

		Assert.False(logger.IsEnabled(LogLevel.Information));
		Assert.True(logger.IsEnabled(LogLevel.Warning));
	}
}
