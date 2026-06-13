// The IStartupRegistration adapter: the single place that touches the Windows registry to manage the
// launch-at-login entry. It writes the current executable path under the current-user
// (HKCU) Run key, so registration needs no elevation. Every operation is idempotent — CreateSubKey +
// SetValue overwrite a single entry, and DeleteValue tolerates a missing value — so repeated toggles
// never leave duplicates or orphans. IsEnabled reads the real key, so the toggle reflects reality.
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

	public bool IsEnabled()
	{
		using RegistryKey? key = Registry.CurrentUser.OpenSubKey(_options.RunKeyPath);
		return key?.GetValue(_options.ValueName) is not null;
	}

	public void Enable()
	{
		using RegistryKey key = Registry.CurrentUser.CreateSubKey(_options.RunKeyPath);
		key.SetValue(_options.ValueName, CommandLine());
	}

	public void Disable()
	{
		using RegistryKey? key = Registry.CurrentUser.OpenSubKey(_options.RunKeyPath, writable: true);
		key?.DeleteValue(_options.ValueName, throwOnMissingValue: false);
	}

	// The command Windows runs at login: the current executable path, quoted so a path with spaces is
	// passed as a single argument.
	private static string CommandLine()
	{
		string? path = Environment.ProcessPath;
		return string.IsNullOrEmpty(path) ? string.Empty : $"\"{path}\"";
	}
}
