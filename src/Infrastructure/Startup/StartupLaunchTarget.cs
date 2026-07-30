// Pure resolution of WHICH executable the launch-at-login entry should name. Kept separate from the
// registry adapter so the rule is unit-testable without a real install layout: the caller supplies the
// running executable's path and a file-existence probe.
//
// Under Velopack the app is installed as <root>\current\<app>.exe with a stable stub launcher at
// <root>\<app>.exe, and an update replaces the whole `current` folder. Registering the running executable
// would therefore pin the Run entry to a path an update can invalidate — and, while `current` is being
// swapped, to a path that briefly does not exist at all. The stub is the stable entry point and always
// resolves the newest installed version, so it is preferred whenever the layout is recognised. Any other
// layout (a dev build, an xcopy deployment) registers the running executable as-is.

namespace Infrastructure.Startup;

public static class StartupLaunchTarget
{
	// The Velopack install layout: the versioned payload lives in this folder, the stub launcher one level up.
	private const string VelopackCurrentFolder = "current";

	/// <summary>Resolves the executable a login launch should run, given the running executable's path and a
	/// probe for whether a candidate file exists. Returns an empty string when the process path is unknown.</summary>
	public static string Resolve(string? processPath, Func<string, bool> fileExists)
	{
		ArgumentNullException.ThrowIfNull(fileExists);

		if (string.IsNullOrEmpty(processPath))
		{
			return string.Empty;
		}

		string? directory = Path.GetDirectoryName(processPath);
		if (directory is null
			|| !string.Equals(Path.GetFileName(directory), VelopackCurrentFolder, StringComparison.OrdinalIgnoreCase))
		{
			return processPath;
		}

		string? installRoot = Path.GetDirectoryName(directory);
		if (installRoot is null)
		{
			return processPath;
		}

		// The stub carries the same file name as the payload executable, one level above `current`.
		string stub = Path.Combine(installRoot, Path.GetFileName(processPath));
		return fileExists(stub) ? stub : processPath;
	}
}
