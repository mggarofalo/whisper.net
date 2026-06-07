// Verifies the Generic Host composition (WHISPER-57): a host built from the per-layer registration
// extensions resolves the Mediator that dispatches handlers, and Serilog's minimum level is honored
// from configuration (proving the layered config -> logging path). The host uses the exact same
// AddWhisperServices / AddSerilogLogging extensions the WPF app calls.

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

	[Fact]
	public void Honors_the_minimum_log_level_from_configuration()
	{
		using IHost host = BuildHost(new KeyValuePair<string, string?>("Serilog:MinimumLevel:Default", "Warning"));

		ILogger<HostCompositionTests> logger = host.Services.GetRequiredService<ILogger<HostCompositionTests>>();

		Assert.False(logger.IsEnabled(LogLevel.Information));
		Assert.True(logger.IsEnabled(LogLevel.Warning));
	}
}
