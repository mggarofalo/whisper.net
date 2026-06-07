// Resolves a stored capture-device selection against the devices currently available. Three cases:
//   * the "system default" sentinel -> follow the OS default (and keep following when it changes);
//   * a pinned id that is present     -> use it;
//   * a pinned id that is missing     -> fall back to the OS default and flag the substitution.
// Pure logic — no device access — so it is fully unit-testable and is what drives both first-launch
// resolution and the hot-swap re-resolution when the default device changes.

using Domain.Audio;

namespace Logic.AudioManagement;

public sealed class DeviceSelectionPolicy
{
	public DeviceResolution Resolve(string selectedId, IReadOnlyList<AudioDevice> available, string? systemDefaultId)
	{
		// Following the OS default: use whatever the current default is (hot-swaps when it changes).
		if (string.IsNullOrEmpty(selectedId) || selectedId == AudioDevice.SystemDefault)
		{
			return new DeviceResolution(systemDefaultId, FollowsDefault: true, Substituted: false);
		}

		// Pinned device that is still present: use it.
		if (available.Any(device => device.Id == selectedId))
		{
			return new DeviceResolution(selectedId, FollowsDefault: false, Substituted: false);
		}

		// Pinned device is gone: fall back to the OS default and report the substitution.
		return new DeviceResolution(systemDefaultId, FollowsDefault: false, Substituted: true);
	}
}
