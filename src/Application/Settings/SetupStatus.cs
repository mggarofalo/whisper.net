// Whether the app is configured enough to run dictation without first-run setup (WHISPER-82). The launch
// flow opens the settings window when the app is NOT configured, and goes straight to the tray when it is.
// "Configured" means the user finished setup AND the chosen model is actually present locally — so a
// completed setup whose model file has since gone missing correctly re-prompts (there is "no active model").

namespace Application.Settings;

public sealed record SetupStatus(bool IsConfigured);
