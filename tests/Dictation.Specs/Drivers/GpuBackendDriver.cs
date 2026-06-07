// The Driver owns HOW the GPU contact point is exercised, so step definitions stay one-liners. It
// configures the faked raw probe (available / unavailable / throwing) and runs the REAL backend
// selector over it, asserting on the reported BackendSelection. Like VadDriver constructs the real
// SileroVad over a fake session, this constructs the real GpuBackendSelector over the faked probe —
// so the actual decision logic is exercised, free of DI-lifetime concerns.

using Application.Ports;
using AwesomeAssertions;
using Domain.Models;
using Logic.GpuContactPoint;
using NSubstitute;

namespace Dictation.Specs.Drivers;

public sealed class GpuBackendDriver(IGpuProbe probe)
{
	private readonly GpuBackendSelector _selector = new(probe);
	private BackendSelection? _selection;

	public void GivenVulkanAvailable() =>
		probe.IsGpuRuntimeAvailableAsync(Arg.Any<CancellationToken>()).Returns(true);

	public void GivenVulkanUnavailable() =>
		probe.IsGpuRuntimeAvailableAsync(Arg.Any<CancellationToken>()).Returns(false);

	public void GivenProbeThrows() =>
		probe.IsGpuRuntimeAvailableAsync(Arg.Any<CancellationToken>())
			.Returns<ValueTask<bool>>(_ => throw new InvalidOperationException("driver init failed"));

	public async Task SelectBackend() =>
		_selection = await _selector.SelectBackendAsync(CancellationToken.None);

	public void AssertGpuBackendChosen() =>
		Selection().Backend.Should().Be(ComputeBackend.Vulkan);

	public void AssertCpuBackendChosen() =>
		Selection().Backend.Should().Be(ComputeBackend.Cpu);

	public void AssertReasonCitesVulkan() =>
		Selection().Reason.Should().Contain("Vulkan");

	// The contact point completing at all — returning a non-null selection rather than throwing or
	// hanging — IS the "continues without hanging or crashing" guarantee.
	public void AssertCompletedWithoutCrashing() =>
		Selection().Should().NotBeNull();

	private BackendSelection Selection() =>
		_selection ?? throw new InvalidOperationException("Select a backend before asserting.");
}
