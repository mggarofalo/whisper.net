// Configuration for the file-backed settings store (WHISPER-43): where the settings file lives. Bound
// from the "Settings" configuration section; when left unset, AddInfrastructure post-configures it to
// a per-user application-data path so a fresh install needs no configuration.

namespace Infrastructure.Settings;

public sealed class SettingsStoreOptions
{
	public const string SectionName = "Settings";

	// Absolute path to the settings file. Defaulted by AddInfrastructure when not configured.
	public string FilePath { get; set; } = string.Empty;
}
