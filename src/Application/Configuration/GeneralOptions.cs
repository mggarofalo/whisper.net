// Strongly-typed binding for general application settings, populated from the "General" configuration
// section by AddApplication. Demonstrates the layered-config -> options pattern; more sections are
// bound the same way as features add settings.

namespace Application.Configuration;

public sealed class GeneralOptions
{
	public const string SectionName = "General";

	// Display name of the product; defaults if no configuration is present.
	public string ApplicationName { get; init; } = "Whisper";
}
