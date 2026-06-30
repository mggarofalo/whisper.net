// Inner TDD loop for DeviceSelectionPolicy: the system-default sentinel (and empty) follow the OS
// default, a present pinned device is used as-is, a pinned id that is gone but whose friendly name is
// present is followed under its new id (self-heal), and a pinned device missing by both id and name
// falls back to the default with the substitution flagged. Also covers re-resolution when there is no
// default at all.

using AwesomeAssertions;
using Domain.Audio;
using Xunit;

namespace Logic.AudioManagement.Tests;

public sealed class DeviceSelectionPolicyTests
{
	private readonly DeviceSelectionPolicy _policy = new();

	private static readonly IReadOnlyList<AudioDevice> TwoDevices =
	[
		new("mic-a", "Mic A"),
		new("mic-b", "Mic B"),
	];

	[Fact]
	public void System_default_sentinel_follows_the_os_default()
	{
		DeviceResolution resolution = _policy.Resolve(AudioDevice.SystemDefault, selectedName: null, TwoDevices, "mic-a");

		resolution.Should().Be(new DeviceResolution("mic-a", FollowsDefault: true, Substituted: false));
	}

	[Fact]
	public void Empty_selection_follows_the_os_default()
	{
		DeviceResolution resolution = _policy.Resolve("", selectedName: null, TwoDevices, "mic-b");

		resolution.Should().Be(new DeviceResolution("mic-b", FollowsDefault: true, Substituted: false));
	}

	[Fact]
	public void A_present_pinned_device_is_used_as_is()
	{
		DeviceResolution resolution = _policy.Resolve("mic-b", selectedName: null, TwoDevices, "mic-a");

		resolution.Should().Be(new DeviceResolution("mic-b", FollowsDefault: false, Substituted: false));
	}

	[Fact]
	public void A_pinned_id_that_changed_is_recovered_by_friendly_name()
	{
		// The endpoint id is gone (a reboot re-enumerated the device under a new id), but the same friendly
		// name is present: follow it under its current id rather than warning or falling back to default.
		DeviceResolution resolution = _policy.Resolve("mic-a-old-id", selectedName: "Mic A", TwoDevices, "mic-b");

		resolution.Should().Be(new DeviceResolution("mic-a", FollowsDefault: false, Substituted: false));
	}

	[Fact]
	public void A_present_id_wins_over_a_name_match()
	{
		// When the id still resolves, it is authoritative even if some other device shares the name.
		DeviceResolution resolution = _policy.Resolve("mic-b", selectedName: "Mic A", TwoDevices, "mic-a");

		resolution.DeviceId.Should().Be("mic-b");
		resolution.Substituted.Should().BeFalse();
	}

	[Fact]
	public void A_device_missing_by_both_id_and_name_falls_back_to_the_default_and_flags_substitution()
	{
		DeviceResolution resolution = _policy.Resolve("mic-gone", selectedName: "Ghost Mic", TwoDevices, "mic-a");

		resolution.Should().Be(new DeviceResolution("mic-a", FollowsDefault: false, Substituted: true));
	}

	[Fact]
	public void A_missing_pinned_device_with_no_name_falls_back_to_the_default_and_flags_substitution()
	{
		DeviceResolution resolution = _policy.Resolve("mic-gone", selectedName: null, TwoDevices, "mic-a");

		resolution.Should().Be(new DeviceResolution("mic-a", FollowsDefault: false, Substituted: true));
	}

	[Fact]
	public void Following_the_default_resolves_to_the_new_default_after_it_changes()
	{
		// Re-resolution with a different default is exactly how a default-device hot-swap works.
		DeviceResolution resolution = _policy.Resolve(AudioDevice.SystemDefault, selectedName: null, TwoDevices, "mic-b");

		resolution.DeviceId.Should().Be("mic-b");
		resolution.FollowsDefault.Should().BeTrue();
	}

	[Fact]
	public void A_missing_pinned_device_with_no_default_resolves_to_nothing()
	{
		DeviceResolution resolution = _policy.Resolve("mic-gone", selectedName: null, TwoDevices, systemDefaultId: null);

		resolution.DeviceId.Should().BeNull();
		resolution.Substituted.Should().BeTrue();
	}
}
