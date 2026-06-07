// A selectable capture device: a stable id (persisted so the same device is restored across restarts)
// and a friendly name for display. The SystemDefault sentinel is stored as the selection when the
// user wants to follow whatever the OS default capture device is, rather than pinning one device.

namespace Domain.Audio;

public sealed record AudioDevice(string Id, string Name)
{
	/// <summary>Sentinel selection id meaning "follow the OS default capture device" rather than a pinned device.</summary>
	public const string SystemDefault = "system-default";
}
