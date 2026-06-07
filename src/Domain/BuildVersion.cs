// Single source of truth for the product version. MinVer injects the version (derived from git
// tags) into the assembly at build time; this exposes it so any layer — and the WHISPER-1
// build-reproducibility scenario — can read one canonical value rather than hard-coding it.

using System.Reflection;

namespace Domain;

public static class BuildVersion
{
	// The MinVer-derived informational version, e.g. "0.1.0" for a tagged build or
	// "0.1.0-alpha.0.5+<sha>" for an untagged one. Falls back to "0.0.0" only if the
	// attribute is somehow absent (never expected in a normal build).
	public static string Informational =>
		typeof(BuildVersion).Assembly
			.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
		?? "0.0.0";
}
