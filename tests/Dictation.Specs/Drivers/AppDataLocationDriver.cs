// Exercises the real Infrastructure composition. The model cache and
// settings-database default paths are applied by AddInfrastructure's PostConfigure, so this drives the
// actual production registration (no configuration → the per-user defaults kick in) and resolves the
// bound options; the logs directory comes from the same WhisperLogPath the Serilog wiring uses. It then
// asserts none of those live inside the Velopack install root (%LOCALAPPDATA%\<PackId>), which is the
// collision that broke install/update when user data — and an open log handle — sat in the install dir.

using AwesomeAssertions;
using Infrastructure.DependencyInjection;
using Infrastructure.Models;
using Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Dictation.Specs.Drivers;

public sealed class AppDataLocationDriver
{
	// The Velopack PackId from build/pack.ps1. Velopack installs to %LOCALAPPDATA%\<PackId>, so no
	// per-user data directory may equal or sit beneath this path on case-insensitive Windows.
	private const string VelopackPackId = "Whisper.Net";

	private string? _logsDirectory;
	private string? _modelCacheDirectory;
	private string? _databasePath;

	private static string InstallRoot => Path.Combine(
		Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), VelopackPackId);

	public void ResolveDataDirectories()
	{
		ServiceCollection services = new();
		services.AddInfrastructure();

		using ServiceProvider provider = services.BuildServiceProvider();

		_modelCacheDirectory = provider.GetRequiredService<IOptions<ModelCacheOptions>>().Value.CacheDirectory;
		_databasePath = provider.GetRequiredService<IOptions<SqlitePersistenceOptions>>().Value.DatabasePath;
		_logsDirectory = WhisperLogPath.DefaultDirectory;
	}

	public void AssertDataRootIsNotThePackId()
	{
		// The per-user data root's leaf folder name must not match the PackId (the install-root leaf),
		// case-insensitively — that equality is exactly what collided before.
		string dataRootName = new DirectoryInfo(Path.GetDirectoryName(_logsDirectory)!).Name;
		dataRootName.Should().NotBe(VelopackPackId, "the data root must not reuse the Velopack PackId folder name");
		string.Equals(dataRootName, VelopackPackId, StringComparison.OrdinalIgnoreCase)
			.Should().BeFalse("the collision is case-insensitive on Windows");
	}

	public void AssertLogsOutsideInstallRoot() => AssertOutsideInstallRoot(_logsDirectory, "the logs directory");

	public void AssertModelCacheOutsideInstallRoot() =>
		AssertOutsideInstallRoot(_modelCacheDirectory, "the model cache directory");

	public void AssertDatabaseOutsideInstallRoot() =>
		AssertOutsideInstallRoot(_databasePath, "the settings database");

	private static void AssertOutsideInstallRoot(string? path, string because)
	{
		path.Should().NotBeNullOrWhiteSpace();
		IsUnder(path!, InstallRoot).Should().BeFalse($"{because} must live outside the Velopack install root");
	}

	// True when candidate equals installRoot or is nested beneath it (case-insensitive, separator-aware).
	private static bool IsUnder(string candidate, string installRoot)
	{
		string normalizedRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(installRoot));
		string normalizedCandidate = Path.TrimEndingDirectorySeparator(Path.GetFullPath(candidate));

		if (string.Equals(normalizedCandidate, normalizedRoot, StringComparison.OrdinalIgnoreCase))
		{
			return true;
		}

		return normalizedCandidate.StartsWith(
			normalizedRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
	}
}
