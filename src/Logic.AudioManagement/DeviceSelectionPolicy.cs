// Resolves a stored capture-device selection against the devices currently available. Cases:
//   * the "system default" sentinel -> follow the OS default (and keep following when it changes);
//   * a pinned id that is present     -> use it;
//   * a pinned id that is gone but the same friendly NAME is present -> follow it under its new id
//     (self-heals an endpoint id that changed across reboots — the common USB/Bluetooth/dock case);
//   * a pinned id that is gone with no name match -> fall back to the OS default and flag the substitution.
// Pure logic — no device access — so it is fully unit-testable and is what drives first-launch
// resolution, the hot-swap re-resolution when the default device changes, and the capture-time choice.

using Domain.Audio;

namespace Logic.AudioManagement;

public sealed class DeviceSelectionPolicy
{
	public DeviceResolution Resolve(
		string selectedId, string? selectedName, IReadOnlyList<AudioDevice> available, string? systemDefaultId)
	{
		// Following the OS default: use whatever the current default is (hot-swaps when it changes).
		if (string.IsNullOrEmpty(selectedId) || selectedId == AudioDevice.SystemDefault)
		{
			return new DeviceResolution(systemDefaultId, FollowsDefault: true, Substituted: false);
		}

		// Pinned device that is still present by id: use it.
		if (available.Any(device => device.Id == selectedId))
		{
			return new DeviceResolution(selectedId, FollowsDefault: false, Substituted: false);
		}

		// The id is gone, but the same friendly name is present: the endpoint id changed (a reboot
		// re-enumerated the device). Follow the device under its new id so the user never has to re-pick.
		if (!string.IsNullOrWhiteSpace(selectedName))
		{
			AudioDevice? byName = available.FirstOrDefault(
				device => string.Equals(device.Name, selectedName, StringComparison.Ordinal));
			if (byName is not null)
			{
				return new DeviceResolution(byName.Id, FollowsDefault: false, Substituted: false);
			}
		}

		// Pinned device is genuinely gone: fall back to the OS default and report the substitution.
		return new DeviceResolution(systemDefaultId, FollowsDefault: false, Substituted: true);
	}
}
