// Formats the MinVer informational version for display: the SemVer version plus a short commit hash.
// The SDK appends the full commit SHA as build metadata ("0.5.3+c9efc2f3a7b1…"), which is too long to read
// at a glance; the first seven characters identify the commit the same way `git log --oneline` does.

namespace Application.Updates;

public static class AppVersionFormatter
{
	private const int ShortHashLength = 7;

	public static string Format(string informationalVersion)
	{
		ArgumentNullException.ThrowIfNull(informationalVersion);

		int plus = informationalVersion.IndexOf('+', StringComparison.Ordinal);
		if (plus < 0)
		{
			return informationalVersion;
		}

		string version = informationalVersion[..plus];
		string metadata = informationalVersion[(plus + 1)..];
		return metadata.Length > ShortHashLength
			? $"{version}+{metadata[..ShortHashLength]}"
			: informationalVersion;
	}
}
