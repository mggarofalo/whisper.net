// The in-memory current settings, made available via DI. The settings lifecycle service
// loads the persisted settings into it during host startup and writes its current value back on
// graceful shutdown; runtime updates (tray/UI in later modules) mutate it so the saved-on-shutdown
// value reflects the latest state. Singleton so every consumer shares one live view of the settings.

using Domain.Settings;

namespace Logic.AppManagement.Settings;

public sealed class SettingsHolder
{
	// Seeded with defaults so the value is always valid before the store has been loaded.
	public AppSettings Current { get; set; } = AppSettings.Default;
}
