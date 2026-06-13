// Strongly-typed binding for the in-app auto-update settings, populated from the "AutoUpdate"
// configuration section. Auto-update is outbound network access, so to honor the privacy
// stance — no network egress without an explicit opt-in — it is OFF by default: nothing is checked or
// downloaded unless the user enables it, and when enabled the release feed below is the only egress and
// no user data is ever sent.

namespace Application.Configuration;

public sealed class AutoUpdateOptions
{
	public const string SectionName = "AutoUpdate";

	/// <summary>Whether the app checks the release feed for a newer version. Opt-in: off by default.</summary>
	public bool Enabled { get; set; }

	/// <summary>The GitHub repository whose Releases host the update feed — the single egress when enabled.</summary>
	public string RepositoryUrl { get; set; } = "https://github.com/mggarofalo/whisper.net";

	/// <summary>Whether pre-release versions are offered (stable releases only by default).</summary>
	public bool IncludePreReleases { get; set; }
}
