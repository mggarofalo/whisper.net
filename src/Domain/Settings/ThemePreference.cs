// The user's chosen app theme (WHISPER-121). System follows the OS Light/Dark preference (the default);
// Light and Dark override it explicitly. Lives in Domain because it is persisted in AppSettings; the
// Presentation layer maps it onto WPF's ThemeMode when applying the theme.

namespace Domain.Settings;

public enum ThemePreference
{
	/// <summary>Follow the operating system's light/dark preference.</summary>
	System,

	/// <summary>Always use the light theme.</summary>
	Light,

	/// <summary>Always use the dark theme.</summary>
	Dark,
}
