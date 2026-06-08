// Boundary projection of the AppSettings domain type for callers (queries/commands and Presentation).
// The hotkey is carried as its canonical chord string rather than the HotkeyBinding value object, so
// the DTO stays free of domain construction rules; the SettingsMapper converts between the two. The
// capture-device selection is defaulted so existing construction sites need not change.

using Domain.Audio;

namespace Application.Settings;

public sealed record AppSettingsDto(
	string ModelId,
	string Hotkey,
	int SilenceThresholdMs,
	bool FillerWordRemovalEnabled,
	string CaptureDeviceId = AudioDevice.SystemDefault,
	bool AuditLogEnabled = false);
