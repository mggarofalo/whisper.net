// Exercises the Velopack packaging configuration. Like the repository-guidance driver, it inspects
// repository artifacts directly rather than driving behavior
// through IMediator — because the packaging contract lives in the project file, the version policy, and
// the packaging script, not in the application's runtime composition. It asserts the configuration that
// makes the installer correct: a self-contained, single-file, native-bundling win-x64 publish; a
// MinVer-derived (never hand-edited) version; and a one-command, reproducibly-pinned `vpk pack`. That the
// installer actually builds and the native assets load is demonstrated by running build/pack.ps1 and the
// produced exe (see the PR); a launch on a clean, runtime-free machine is tracked as a follow-up.

using AwesomeAssertions;

namespace Dictation.Specs.Drivers;

public sealed class PackagingDriver
{
	private static readonly string RepositoryRoot = FindRepositoryRoot();

	private string Read(params string[] relativeParts) =>
		File.ReadAllText(Path.Combine(new[] { RepositoryRoot }.Concat(relativeParts).ToArray()));

	private bool Exists(params string[] relativeParts) =>
		File.Exists(Path.Combine(new[] { RepositoryRoot }.Concat(relativeParts).ToArray()));

	private string PresentationProject => Read("src", "Presentation", "Presentation.csproj");
	private string BuildProps => Read("Directory.Build.props");
	private string PackScript => Read("build", "pack.ps1");
	private string ToolManifest => Read(".config", "dotnet-tools.json");

	// --- self-contained single-file build ---

	public void AssertPublishesSelfContainedSingleFileWinX64()
	{
		// The publish settings turn on only when a RID is supplied (the script/CI pass -r win-x64), so dev
		// builds stay unaffected; assert that conditioned group and its self-contained/single-file flags.
		string project = PresentationProject;
		project.Should().Contain("'$(RuntimeIdentifier)' != ''");
		project.Should().Contain("<SelfContained>true</SelfContained>");
		project.Should().Contain("<PublishSingleFile>true</PublishSingleFile>");
		PackScript.Should().Contain("win-x64");
	}

	public void AssertNativeAssetsLooseForTheLoader()
	{
		// The native libraries must NOT be embedded for self-extract — Whisper.net's loader
		// resolves whisper.dll relative to the base directory and cannot find it in the self-extract temp
		// dir, which silently broke all transcription. They stay loose (runtimes/win-x64/native) next to the
		// single-file exe, where the loader finds them; the self-contained publish still copies them.
		string project = PresentationProject;
		project.Should().Contain("<IncludeNativeLibrariesForSelfExtract>false</IncludeNativeLibrariesForSelfExtract>");
		project.Should().Contain("<SelfContained>true</SelfContained>");
	}

	// --- version from MinVer, not hand-edited ---

	public void AssertNoStaticAssemblyVersionCommitted()
	{
		// MinVer owns the version; a committed <Version>/<AssemblyVersion> element would defeat that. Match
		// the closing tag so prose that merely mentions "<Version>" (the explanatory comment) does not trip.
		BuildProps.Should().NotContain("</Version>");
		BuildProps.Should().NotContain("</AssemblyVersion>");
		PresentationProject.Should().NotContain("</Version>");
	}

	public void AssertVersionDerivedFromMinVer() =>
		BuildProps.Should().Contain("<MinVerTagPrefix>v</MinVerTagPrefix>");

	public void AssertPackScriptReadsVersionFromMinVer()
	{
		// The script asks MinVer for the version rather than carrying a literal package version.
		string script = PackScript;
		script.Should().Contain("minver");
		script.Should().Contain("--packVersion $packVersion");
		script.Should().NotMatchRegex(@"--packVersion\s+\d");
	}

	// --- reproducible Velopack installer ---

	public void AssertOneCommandScriptBuildsVelopackInstaller()
	{
		Exists("build", "pack.ps1").Should().BeTrue();
		PackScript.Should().Contain("vpk pack");
	}

	public void AssertAppIdAndIconAreSet()
	{
		string script = PackScript;
		script.Should().Contain("--packId");
		script.Should().Contain("--icon");
		Exists("assets", "whisper.ico").Should().BeTrue();
		PresentationProject.Should().Contain("<ApplicationIcon>");
	}

	public void AssertVpkToolIsPinned()
	{
		ToolManifest.Should().Contain("\"vpk\"");
		ToolManifest.Should().Contain("\"version\"");
	}

	private static string FindRepositoryRoot()
	{
		DirectoryInfo? directory = new(AppContext.BaseDirectory);
		while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Whisper.slnx")))
		{
			directory = directory.Parent;
		}

		return directory?.FullName
			?? throw new InvalidOperationException("Could not locate the repository root (Whisper.slnx).");
	}
}
