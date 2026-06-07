// Inner TDD loop for DeviceSelectionPolicy: the system-default sentinel (and empty) follow the OS
// default, a present pinned device is used as-is, and a missing pinned device falls back to the
// default with the substitution flagged. Also covers re-resolution when there is no default at all.

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
		DeviceResolution resolution = _policy.Resolve(AudioDevice.SystemDefault, TwoDevices, "mic-a");

		resolution.Should().Be(new DeviceResolution("mic-a", FollowsDefault: true, Substituted: false));
	}

	[Fact]
	public void Empty_selection_follows_the_os_default()
	{
		DeviceResolution resolution = _policy.Resolve("", TwoDevices, "mic-b");

		resolution.Should().Be(new DeviceResolution("mic-b", FollowsDefault: true, Substituted: false));
	}

	[Fact]
	public void A_present_pinned_device_is_used_as_is()
	{
		DeviceResolution resolution = _policy.Resolve("mic-b", TwoDevices, "mic-a");

		resolution.Should().Be(new DeviceResolution("mic-b", FollowsDefault: false, Substituted: false));
	}

	[Fact]
	public void A_missing_pinned_device_falls_back_to_the_default_and_flags_substitution()
	{
		DeviceResolution resolution = _policy.Resolve("mic-gone", TwoDevices, "mic-a");

		resolution.Should().Be(new DeviceResolution("mic-a", FollowsDefault: false, Substituted: true));
	}

	[Fact]
	public void Following_the_default_resolves_to_the_new_default_after_it_changes()
	{
		// Re-resolution with a different default is exactly how a default-device hot-swap works.
		DeviceResolution resolution = _policy.Resolve(AudioDevice.SystemDefault, TwoDevices, "mic-b");

		resolution.DeviceId.Should().Be("mic-b");
		resolution.FollowsDefault.Should().BeTrue();
	}

	[Fact]
	public void A_missing_pinned_device_with_no_default_resolves_to_nothing()
	{
		DeviceResolution resolution = _policy.Resolve("mic-gone", TwoDevices, systemDefaultId: null);

		resolution.DeviceId.Should().BeNull();
		resolution.Substituted.Should().BeTrue();
	}
}
