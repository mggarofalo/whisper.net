// Configuration for the registry-backed run-on-login registration (WHISPER-32): which current-user
// (HKCU) subkey and value name to use. Defaults to the standard Windows Run key; tests point it at a
// throwaway subkey so they never touch the real startup list.

namespace Infrastructure.Startup;

public sealed class StartupRegistrationOptions
{
	public const string SectionName = "Startup";

	// The HKCU subkey holding launch-at-login entries.
	public string RunKeyPath { get; set; } = @"Software\Microsoft\Windows\CurrentVersion\Run";

	// The value name under that key identifying this app's entry.
	public string ValueName { get; set; } = "Whisper";
}
