// Why a capture stopped unexpectedly. The capture port reports these as a typed failure rather than
// throwing on the audio thread, so callers can react (notify the user, fall back to another device)
// instead of crashing the recording flow.

namespace Domain.Audio;

public enum AudioCaptureError
{
	/// <summary>The capture device was removed or became unavailable mid-session.</summary>
	DeviceUnavailable,

	/// <summary>The device could not be opened because exclusive-mode access was denied.</summary>
	ExclusiveModeDenied,

	/// <summary>Capture stopped for an unclassified reason.</summary>
	Unknown,
}
