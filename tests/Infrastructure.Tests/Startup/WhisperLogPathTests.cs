// Inner TDD loop for the log-path helper. The installed tray app must write its logs to a
// single, well-known per-user location so a bug report can attach them; these pin that contract: logs
// live under LocalApplicationData\whisper-net\logs, beside the model cache, with a daily-rolling name.
// (The folder is "whisper-net", not "whisper.net": the old name collided with the Velopack install root.)

using AwesomeAssertions;
using Infrastructure.DependencyInjection;
using Xunit;

namespace Infrastructure.Tests.Startup;

public sealed class WhisperLogPathTests
{
	[Fact]
	public void Default_directory_is_under_local_application_data_whisper_net_logs()
	{
		string expected = Path.Combine(
			Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
			"whisper-net",
			"logs");

		WhisperLogPath.DefaultDirectory.Should().Be(expected);
	}

	[Fact]
	public void File_name_template_rolls_daily_with_a_log_extension()
	{
		// Serilog substitutes the date into the trailing '-' before the extension (e.g. whisper-20260608.log).
		WhisperLogPath.FileNameTemplate.Should().Be("whisper-.log");
		Path.GetExtension(WhisperLogPath.FileNameTemplate).Should().Be(".log");
	}
}
