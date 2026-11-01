// Port for facts about the running build. Implemented in Infrastructure over the MinVer-stamped
// assembly attribute (Domain.BuildVersion); faked in specs so the version display can be driven with any
// version string.

namespace Application.Ports;

public interface IBuildInfo
{
	/// <summary>The MinVer-derived informational version, e.g. <c>0.5.3+c9efc2f…</c> (full commit hash).</summary>
	string InformationalVersion { get; }
}
