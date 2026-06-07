// The user's configuration as the domain understands it: which model to run, the dictation hotkey,
// how much trailing silence to tolerate, whether filler words are stripped, and which capture device
// to record from. Immutable — a change produces a new AppSettings via `with`, and every instance is
// guaranteed valid because the constructor enforces the invariants the settings validator
// (Application) and store depend on.

using Domain.Audio;

namespace Domain.Settings;

public sealed record AppSettings
{
	public string ModelId { get; }
	public HotkeyBinding Hotkey { get; }
	public int SilenceThresholdMs { get; }
	public bool FillerWordRemovalEnabled { get; }

	// The persisted capture-device selection: a stable device id, or AudioDevice.SystemDefault to
	// follow the OS default. Defaulted so existing construction sites need not change.
	public string CaptureDeviceId { get; }

	public AppSettings(
		string modelId,
		HotkeyBinding hotkey,
		int silenceThresholdMs,
		bool fillerWordRemovalEnabled,
		string captureDeviceId = AudioDevice.SystemDefault)
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

		if (string.IsNullOrWhiteSpace(captureDeviceId))
		{
			throw new DomainException("Capture device selection must not be empty.");
		}

		ModelId = modelId;
		Hotkey = hotkey;
		SilenceThresholdMs = silenceThresholdMs;
		FillerWordRemovalEnabled = fillerWordRemovalEnabled;
		CaptureDeviceId = captureDeviceId;
	}

	// The settings a fresh install starts from.
	public static AppSettings Default { get; } =
		new("base.en", HotkeyBinding.Parse("Ctrl+Win"), silenceThresholdMs: 500, fillerWordRemovalEnabled: true);
}
