// Pins the per-user data-directory resolution so it can never again equal — or sit
// beneath — the Velopack install root (%LOCALAPPDATA%\<PackId>). That collision is what broke
// install/update: the installer's "remove existing application directory" step tripped over user data,
// and an open rolling-log handle inside the install dir blocked updates while the app ran. If anyone
// renames the data folder back to the PackId (or nests data under the install root), these go red.

using AwesomeAssertions;
using Infrastructure.DependencyInjection;
using Xunit;

namespace Infrastructure.Tests.Startup;

public sealed class WhisperAppDataTests
{
	[Fact]
	public void Data_root_folder_name_is_not_the_velopack_pack_id()
	{
		// Case-insensitive: Windows treats "whisper.net" and "Whisper.Net" as the same path.
		WhisperAppData.FolderName.Should().NotBeEquivalentTo(
			WhisperAppData.VelopackPackId,
			"the data folder must never reuse the Velopack PackId — that is the install-dir collision");

		string.Equals(WhisperAppData.FolderName, WhisperAppData.VelopackPackId, StringComparison.OrdinalIgnoreCase)
			.Should().BeFalse();
	}

	[Theory]
	[MemberData(nameof(ResolvedDataPaths))]
	public void Resolved_data_paths_live_outside_the_velopack_install_root(string label, string path)
	{
		_ = label; // names the case in test output
		IsUnder(path, WhisperAppData.VelopackInstallRoot)
			.Should().BeFalse("a per-user data path must never live inside the Velopack install root");
	}

	[Fact]
	public void Logs_and_model_cache_share_the_machine_local_data_root()
	{
		// Consistency (AC3): logs and the model cache both hang off the one LocalApplicationData root.
		WhisperAppData.LogsDirectory.Should().StartWith(WhisperAppData.LocalRoot);
		WhisperAppData.ModelCacheDirectory.Should().StartWith(WhisperAppData.LocalRoot);
		WhisperAppData.DatabasePath.Should().StartWith(WhisperAppData.RoamingRoot);
	}

	public static TheoryData<string, string> ResolvedDataPaths() => new()
	{
		{ "logs", WhisperAppData.LogsDirectory },
		{ "model cache", WhisperAppData.ModelCacheDirectory },
		{ "settings database", WhisperAppData.DatabasePath },
		{ "local root", WhisperAppData.LocalRoot },
	};

	// True when candidate equals installRoot or is nested beneath it (case-insensitive, separator-aware).
	private static bool IsUnder(string candidate, string installRoot)
	{
		string normalizedRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(installRoot));
		string normalizedCandidate = Path.TrimEndingDirectorySeparator(Path.GetFullPath(candidate));

		return string.Equals(normalizedCandidate, normalizedRoot, StringComparison.OrdinalIgnoreCase)
			|| normalizedCandidate.StartsWith(
				normalizedRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
	}
}
