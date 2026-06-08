// Exercises the self-signed code-signing path and the build-from-source guide for the @WHISPER-72
// scenarios. Like the packaging and release-workflow drivers, it inspects repository artifacts directly —
// the signing-cert helper script, the build-and-run guide, and the README — because the contract here is
// "a reproducible script produces the base64 PFX + password that pack.ps1 already consumes, and the path
// is documented", not runtime behavior. A self-signed certificate makes the signature valid only where the
// cert is trusted; it does not earn SmartScreen reputation (that is WHISPER-69's CA cert), and the guide
// must say so.

using AwesomeAssertions;

namespace Dictation.Specs.Drivers;

public sealed class SelfSignedSigningDriver
{
	private static readonly string RepositoryRoot = FindRepositoryRoot();

	private string Read(params string[] relativeParts) =>
		File.ReadAllText(Path.Combine(new[] { RepositoryRoot }.Concat(relativeParts).ToArray()));

	private bool Exists(params string[] relativeParts) =>
		File.Exists(Path.Combine(new[] { RepositoryRoot }.Concat(relativeParts).ToArray()));

	private string SigningScript => Read("build", "new-self-signed-cert.ps1");
	private string BuildAndRunGuide => Read("docs", "build-and-run.md");
	private string Readme => Read("README.md");

	// --- AC1: the script emits what pack.ps1 consumes, and commits no secret ---

	public void AssertSigningScriptEmitsPfxAndPasswordForPackScript()
	{
		Exists("build", "new-self-signed-cert.ps1").Should().BeTrue("the self-signed signing helper must exist");
		string script = SigningScript;
		// It mints a self-signed code-signing cert and exports a password-protected PFX...
		script.Should().Contain("New-SelfSignedCertificate");
		script.Should().Contain("CodeSigningCert");
		script.Should().Contain("Export-PfxCertificate");
		// ...and surfaces exactly the two environment values build/pack.ps1 reads.
		script.Should().Contain("VELOPACK_SIGN_CERTIFICATE");
		script.Should().Contain("VELOPACK_SIGN_PASSWORD");
		// The certificate handed to pack.ps1 is base64 of the PFX bytes.
		script.Should().Contain("ToBase64String");
	}

	public void AssertSigningScriptCommitsNoCertificateOrKey()
	{
		// No PFX/PEM/key material may be checked in, and .gitignore must keep generated certs out of the tree.
		foreach (string ext in new[] { "*.pfx", "*.p12", "*.pem", "*.snk" })
		{
			Directory.EnumerateFiles(RepositoryRoot, ext, SearchOption.AllDirectories)
				.Where(p => !p.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}")
					&& !p.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}"))
				.Should().BeEmpty($"no {ext} secret material may be committed");
		}

		Read(".gitignore").Should().Contain("*.pfx", "generated signing certs must be git-ignored");
	}

	// --- AC2: the script can trust the cert locally so signtool verify /pa passes ---

	public void AssertSigningScriptCanTrustCertificateLocally()
	{
		string script = SigningScript;
		// An opt-in trust step imports the cert into the local store so `signtool verify /pa` passes and UAC
		// shows the publisher; mention signtool verify so the contract with the docs is explicit.
		script.Should().Contain("-Trust");
		script.Should().Contain("Import-Certificate");
		script.Should().Contain("signtool verify");
	}

	// --- AC3: documented build-from-source + self-signed signing + run, linked from README ---

	public void AssertBuildAndRunGuideDocumentsBuildSignAndRun()
	{
		Exists("docs", "build-and-run.md").Should().BeTrue("the build-and-run guide must exist");
		string guide = BuildAndRunGuide;
		guide.Should().Contain("dotnet build", "the guide documents building from source");
		guide.Should().Contain("new-self-signed-cert.ps1", "the guide documents the self-signed signing path");
		guide.Should().Contain("pack.ps1", "the guide documents producing the installer");
		guide.Should().Contain("signtool verify", "the guide documents verifying the signature");
	}

	public void AssertBuildAndRunGuideIsHonestAboutSmartScreen() =>
		BuildAndRunGuide.Should().Contain("SmartScreen",
			"the guide must be honest that self-signed signing does not earn SmartScreen trust");

	public void AssertReadmeLinksToBuildAndRunGuide() =>
		Readme.Should().Contain("docs/build-and-run.md", "the README must link to the build-and-run guide");

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
