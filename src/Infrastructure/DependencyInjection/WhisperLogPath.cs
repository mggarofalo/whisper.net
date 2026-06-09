// The on-disk location of the application's log files (WHISPER-73). Logs live beside the model cache under
// LocalApplicationData (machine-local, not roaming) so a bug report from an installed tray app has a single
// well-known place to attach. Kept tiny and free of Serilog so it is trivially unit-testable and reusable.

namespace Infrastructure.DependencyInjection;

public static class WhisperLogPath
{
	/// <summary>The per-user logs directory: <c>%LOCALAPPDATA%\whisper.net\logs</c>.</summary>
	public static string DefaultDirectory => Path.Combine(
		Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
		"whisper.net",
		"logs");

	/// <summary>The rolling log-file name template Serilog rolls daily (e.g. <c>whisper-20260608.log</c>).</summary>
	public const string FileNameTemplate = "whisper-.log";

	/// <summary>The configuration key that overrides <see cref="DefaultDirectory"/> (used by tests).</summary>
	public const string DirectoryConfigurationKey = "Serilog:LogDirectory";
}
