// Exercises the tag-driven release workflow. Like the packaging and
// repository-guidance drivers, it inspects the repository artifact directly — `.github/workflows/
// release.yml` — because the release contract lives in that workflow, not in the app's runtime
// composition. It asserts the guarantees that make a release correct and safe: it runs only on a version
// tag; it builds warnings-as-errors and tests BEFORE it packages or publishes (so a broken build never
// ships); the version comes from MinVer (the tag); it publishes the Velopack installer + update package
// to a GitHub Release; and signing secrets come from Actions secrets and are never echoed. Actually
// pushing a tag to cut a live release is an outward-facing action tracked separately as a follow-up.

using AwesomeAssertions;

namespace Dictation.Specs.Drivers;

public sealed class ReleaseWorkflowDriver
{
	private static readonly string RepositoryRoot = FindRepositoryRoot();

	private string Workflow => File.ReadAllText(Path.Combine(RepositoryRoot, ".github", "workflows", "release.yml"));

	// --- trigger ---

	public void AssertTriggersOnlyOnVersionTags()
	{
		string workflow = Workflow;
		workflow.Should().Contain("tags:");
		workflow.Should().Contain("v*.*.*");
	}

	public void AssertDoesNotRunOnPullRequestsOrBranches()
	{
		string workflow = Workflow;
		workflow.Should().NotContain("pull_request");
		workflow.Should().NotContain("branches:");
	}

	// --- build + test gate ---

	public void AssertBuildsWarningsAsErrorsAndTests()
	{
		string workflow = Workflow;
		workflow.Should().Contain("TreatWarningsAsErrors=true");
		workflow.Should().Contain("dotnet test");
	}

	public void AssertBuildsAndTestsBeforePackagingOrPublishing()
	{
		string workflow = Workflow;
		int test = workflow.IndexOf("dotnet test", StringComparison.Ordinal);
		int pack = workflow.IndexOf("pack.ps1", StringComparison.Ordinal);
		int publish = workflow.IndexOf("action-gh-release", StringComparison.Ordinal);

		test.Should().BeGreaterThan(0);
		pack.Should().BeGreaterThan(test, "the build/test gate must run before packaging");
		publish.Should().BeGreaterThan(pack, "publishing must come after packaging");
	}

	public void AssertNoBuildOrTestStepContinuesOnError() =>
		Workflow.Should().NotContain("continue-on-error");

	// --- versioning + publish ---

	public void AssertVersionDerivedFromTagByMinVer()
	{
		string workflow = Workflow;
		// pack.ps1 resolves the version from MinVer; the full-history checkout is what lets MinVer see the tag.
		workflow.Should().Contain("pack.ps1");
		workflow.Should().Contain("fetch-depth: 0");
	}

	public void AssertPackagesWithVelopack() =>
		Workflow.Should().Contain("pack.ps1");

	public void AssertPublishesInstallerAndUpdatePackageToARelease()
	{
		string workflow = Workflow;
		workflow.Should().Contain("action-gh-release");
		workflow.Should().Contain("-Setup.exe");
		workflow.Should().Contain("-full.nupkg");
	}

	// --- signing secrets ---

	public void AssertSigningSecretsInjectedFromActionsSecrets() =>
		Workflow.Should().MatchRegex(@"\$\{\{\s*secrets\.\w*SIGN\w*\s*\}\}");

	public void AssertNoSecretIsEchoed()
	{
		// A release log must never print a secret; guard the obvious leak of echoing a secrets expression.
		Workflow.Should().NotMatchRegex(@"echo[^\n]*secrets\.");
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
