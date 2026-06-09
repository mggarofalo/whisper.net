// Exercises the real Serilog wiring for the @WHISPER-73 scenarios. Unlike the artifact-inspecting
// packaging drivers, this drives the actual AddSerilogLogging composition: it points the log directory at
// a temp folder via configuration, resolves an ILogger<T> from the built provider, logs, then disposes the
// provider (which flushes the file sink) and asserts the event landed in a rolling .log file. That proves
// the installed app produces a persistent log on disk, not just an invisible console line.

using AwesomeAssertions;
using Infrastructure.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Dictation.Specs.Drivers;

public sealed class LoggingDriver : IDisposable
{
	private string? _logDirectory;
	private string? _loggedMessage;

	public void ConfigureLoggingToATempDirectory()
	{
		_logDirectory = Path.Combine(Path.GetTempPath(), "whisper-logtest-" + Guid.NewGuid().ToString("N"));
	}

	public void LogAnInformationalEvent()
	{
		_loggedMessage = "diagnosable-event-" + Guid.NewGuid().ToString("N");

		IConfiguration configuration = new ConfigurationBuilder()
			.AddInMemoryCollection(new Dictionary<string, string?>
			{
				["Serilog:LogDirectory"] = _logDirectory,
			})
			.Build();

		ServiceCollection services = new();
		services.AddSerilogLogging(configuration);

		// Dispose the provider before asserting: AddSerilog(dispose: true) disposes the logger on provider
		// disposal, which flushes and releases the file sink so the file is readable.
		using (ServiceProvider provider = services.BuildServiceProvider())
		{
			ILogger<LoggingDriver> logger = provider.GetRequiredService<ILogger<LoggingDriver>>();
			logger.LogInformation("{Marker}", _loggedMessage);
		}
	}

	public void AssertEventWrittenToRollingLogFile()
	{
		_logDirectory.Should().NotBeNull();
		Directory.Exists(_logDirectory).Should().BeTrue("the log directory must be created");

		string[] logFiles = Directory.GetFiles(_logDirectory!, "*.log");
		logFiles.Should().NotBeEmpty("a rolling log file must be written to the configured directory");

		string contents = string.Concat(logFiles.Select(File.ReadAllText));
		contents.Should().Contain(_loggedMessage!, "the logged event must be persisted to the file");
	}

	public void Dispose()
	{
		if (_logDirectory is not null && Directory.Exists(_logDirectory))
		{
			try
			{
				Directory.Delete(_logDirectory, recursive: true);
			}
			catch (IOException)
			{
				// A held file handle on a slow CI agent must not fail the scenario; the temp dir is disposable.
			}
		}
	}
}
