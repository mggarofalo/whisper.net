// Port for discovering capture devices. Implemented in Infrastructure over NAudio's MMDeviceEnumerator
// (Module 2); faked in specs so device-selection policy can be driven without real hardware.

using Domain.Audio;

namespace Application.Ports;

/// <summary>Lists the capture devices currently available and identifies the OS default.</summary>
public interface IAudioDeviceEnumerator
{
	/// <summary>The active capture devices (id + friendly name).</summary>
	IReadOnlyList<AudioDevice> GetCaptureDevices();

	/// <summary>The id of the system default capture device, or <c>null</c> if there is none.</summary>
	string? GetSystemDefaultId();
}
