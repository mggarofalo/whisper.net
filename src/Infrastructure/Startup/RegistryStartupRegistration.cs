// The IStartupRegistration adapter: the single place that touches the Windows registry to manage the
// launch-at-login entry. It writes the launch command under the current-user (HKCU) Run key, so
// registration needs no elevation. Every operation is idempotent — CreateSubKey + SetValue overwrite a
// single entry, and DeleteValue tolerates a missing value — so repeated toggles never leave duplicates or
// orphans. IsEnabled reads the real key, so the toggle reflects reality.
//
// Which executable gets registered matters, and that rule lives in StartupLaunchTarget so it can be tested
// without a real install layout: under Velopack the stable stub launcher is preferred over the versioned
// payload, so an update cannot invalidate the Run entry.
//
// Windows-only by nature; annotated accordingly because Infrastructure targets portable net10.0.

using System.Runtime.Versioning;
using Application.Ports;
using Microsoft.Extensions.Options;
using Microsoft.Win32;

namespace Infrastructure.Startup;

[SupportedOSPlatform("windows")]
public sealed class RegistryStartupRegistration(IOptions<StartupRegistrationOptions> options) : IStartupRegistration
{
	private readonly StartupRegistrationOptions _options = options.Value;

	public string ExpectedCommand => Quote(StartupLaunchTarget.Resolve(Environment.ProcessPath, File.Exists));

	public string? RegisteredCommand
	{
		get
		{
			using RegistryKey? key = Registry.CurrentUser.OpenSubKey(_options.RunKeyPath);
			return key?.GetValue(_options.ValueName) as string;
		}
	}

	public bool RegisteredTargetExists
	{
		get
		{
			string? executable = ExecutablePath(RegisteredCommand);
			return !string.IsNullOrEmpty(executable) && File.Exists(executable);
		}
	}

	public bool IsEnabled() => RegisteredCommand is not null;

	public void Enable()
	{
		using RegistryKey key = Registry.CurrentUser.CreateSubKey(_options.RunKeyPath);
		key.SetValue(_options.ValueName, ExpectedCommand);
	}

	public void Disable()
	{
		using RegistryKey? key = Registry.CurrentUser.OpenSubKey(_options.RunKeyPath, writable: true);
		key?.DeleteValue(_options.ValueName, throwOnMissingValue: false);
	}

	// Quoted so a path containing spaces is passed to Windows as a single argument.
	private static string Quote(string path) => string.IsNullOrEmpty(path) ? string.Empty : $"\"{path}\"";

	// Recovers the executable path from a stored Run command, which may be quoted and may carry arguments.
	private static string? ExecutablePath(string? command)
	{
		if (string.IsNullOrWhiteSpace(command))
		{
			return null;
		}

		string trimmed = command.Trim();
		if (trimmed.StartsWith('"'))
		{
			int closing = trimmed.IndexOf('"', startIndex: 1);
			return closing > 1 ? trimmed[1..closing] : null;
		}

		// Unquoted: the whole value is the path when it resolves, otherwise everything up to the first space
		// (an unquoted path containing spaces is ambiguous, and Windows itself resolves it left to right).
		if (File.Exists(trimmed))
		{
			return trimmed;
		}

		int space = trimmed.IndexOf(' ');
		return space > 0 ? trimmed[..space] : trimmed;
	}
}
