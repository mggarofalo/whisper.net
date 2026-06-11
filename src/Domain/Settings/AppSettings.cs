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

	// Opt-in verbose audit logging (WHISPER-34). Privacy-sensitive, so it is OFF by default; nothing is
	// written to the audit log unless the user explicitly enables it. Defaulted so existing sites need not change.
	public bool AuditLogEnabled { get; }

	// Whether first-run onboarding has been completed (WHISPER-51). False on a fresh install, so the
	// onboarding flow is shown until the user finishes it. Defaulted so existing sites need not change.
	public bool SetupCompleted { get; }

	// The chosen app theme (WHISPER-121): System (follow the OS), Light, or Dark. Defaulted to System so
	// existing sites need not change and a fresh install follows the OS preference.
	public ThemePreference ThemePreference { get; }

	public AppSettings(
		string modelId,
		HotkeyBinding hotkey,
		int silenceThresholdMs,
		bool fillerWordRemovalEnabled,
		string captureDeviceId = AudioDevice.SystemDefault,
		bool auditLogEnabled = false,
		bool setupCompleted = false,
		ThemePreference themePreference = ThemePreference.System)
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
		AuditLogEnabled = auditLogEnabled;
		SetupCompleted = setupCompleted;
		ThemePreference = themePreference;
	}

	// The settings a fresh install starts from.
	public static AppSettings Default { get; } =
		new("base.en", HotkeyBinding.Parse("Ctrl+Win"), silenceThresholdMs: 500, fillerWordRemovalEnabled: true);
}
