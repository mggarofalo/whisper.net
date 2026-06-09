// Registers Serilog as the single logging provider for the application. The minimum level (and any
// other Serilog settings) are read from the layered IConfiguration; a console sink and a rolling file
// sink are always added. The file sink (WHISPER-73) is what makes the installed, windowless tray app
// diagnosable: without it the console sink writes into a console no one can see, so failures vanished
// without a trace. Other MS logging providers are cleared so Serilog is authoritative — which is what
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
		// Logs go to a per-user directory (overridable via configuration for tests); created if missing so the
		// first launch on a fresh install has somewhere to write.
		string logDirectory = configuration[WhisperLogPath.DirectoryConfigurationKey] is { Length: > 0 } configured
			? configured
			: WhisperLogPath.DefaultDirectory;
		Directory.CreateDirectory(logDirectory);
		string logFile = Path.Combine(logDirectory, WhisperLogPath.FileNameTemplate);

		Serilog.Core.Logger logger = new LoggerConfiguration()
			.ReadFrom.Configuration(configuration)
			.Enrich.FromLogContext()
			.WriteTo.Console()
			// A daily rolling file with a size cap and a 14-file retention window — enough to triage a bug
			// report without growing unbounded. `shared` lets a second instance write the same day's file.
			.WriteTo.File(
				logFile,
				rollingInterval: RollingInterval.Day,
				fileSizeLimitBytes: 16 * 1024 * 1024,
				rollOnFileSizeLimit: true,
				retainedFileCountLimit: 14,
				shared: true)
			.CreateLogger();

		services.AddLogging(builder =>
		{
			builder.ClearProviders();
			builder.AddSerilog(logger, dispose: true);
		});

		return services;
	}
}
