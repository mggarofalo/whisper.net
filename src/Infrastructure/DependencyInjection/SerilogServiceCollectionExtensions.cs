// Registers Serilog as the single logging provider for the application. The minimum level (and any
// other Serilog settings) are read from the layered IConfiguration; a console sink is always added as
// the default. Other MS logging providers are cleared so Serilog is authoritative — which is what
// makes ILogger<T>.IsEnabled(...) reflect the configured Serilog level. Used by the Generic Host and
// the host integration tests.

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;

namespace Infrastructure.DependencyInjection;

public static class SerilogServiceCollectionExtensions
{
	public static IServiceCollection AddSerilogLogging(this IServiceCollection services, IConfiguration configuration)
	{
		Serilog.Core.Logger logger = new LoggerConfiguration()
			.ReadFrom.Configuration(configuration)
			.Enrich.FromLogContext()
			.WriteTo.Console()
			.CreateLogger();

		services.AddLogging(builder =>
		{
			builder.ClearProviders();
			builder.AddSerilog(logger, dispose: true);
		});

		return services;
	}
}
