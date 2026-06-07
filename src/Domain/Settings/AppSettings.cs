// The user's configuration as the domain understands it: which model to run, the dictation hotkey,
// how much trailing silence to tolerate, and whether filler words are stripped. Immutable — a change
// produces a new AppSettings via `with`, and every instance is guaranteed valid because the
// constructor enforces the invariants the settings validator (Application) and store depend on.

namespace Domain.Settings;

public sealed record AppSettings
{
	public string ModelId { get; }
	public HotkeyBinding Hotkey { get; }
	public int SilenceThresholdMs { get; }
	public bool FillerWordRemovalEnabled { get; }

	public AppSettings(string modelId, HotkeyBinding hotkey, int silenceThresholdMs, bool fillerWordRemovalEnabled)
	{
		if (string.IsNullOrWhiteSpace(modelId))
		{
			throw new DomainException("Selected model id must not be empty.");
		}

		if (hotkey is null)
		{
			throw new DomainException("A hotkey binding is required.");
		}

		if (silenceThresholdMs < 0)
		{
			throw new DomainException("Silence threshold must not be negative.");
		}

		ModelId = modelId;
		Hotkey = hotkey;
		SilenceThresholdMs = silenceThresholdMs;
		FillerWordRemovalEnabled = fillerWordRemovalEnabled;
	}

	// The settings a fresh install starts from.
	public static AppSettings Default { get; } =
		new("base.en", HotkeyBinding.Parse("Ctrl+Win"), silenceThresholdMs: 500, fillerWordRemovalEnabled: true);
}
