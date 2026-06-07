// Inner TDD loop for GpuBackendSelector (the GPU contact point): a present probe selects Vulkan; an
// absent probe falls back to CPU; a throwing probe also falls back to CPU rather than crashing; and
// cancellation propagates cooperatively.

using Application.Ports;
using AwesomeAssertions;
using Domain.Models;
using Logic.GpuContactPoint;
using NSubstitute;
using Xunit;

namespace Logic.GpuContactPoint.Tests;

public sealed class GpuBackendSelectorTests
{
	private readonly IGpuProbe _probe = Substitute.For<IGpuProbe>();

	private GpuBackendSelector CreateSelector() => new(_probe);

	[Fact]
	public async Task Selects_vulkan_when_the_probe_reports_a_runtime()
	{
		_probe.IsGpuRuntimeAvailableAsync(Arg.Any<CancellationToken>()).Returns(true);

		BackendSelection selection = await CreateSelector().SelectBackendAsync(CancellationToken.None);

		selection.Backend.Should().Be(ComputeBackend.Vulkan);
		selection.IsGpu.Should().BeTrue();
		selection.Reason.Should().Contain("Vulkan");
	}

	[Fact]
	public async Task Falls_back_to_cpu_when_no_runtime_is_present()
	{
		_probe.IsGpuRuntimeAvailableAsync(Arg.Any<CancellationToken>()).Returns(false);

		BackendSelection selection = await CreateSelector().SelectBackendAsync(CancellationToken.None);

		selection.Backend.Should().Be(ComputeBackend.Cpu);
		selection.IsGpu.Should().BeFalse();
	}

	[Fact]
	public async Task Falls_back_to_cpu_when_the_probe_throws()
	{
		_probe.IsGpuRuntimeAvailableAsync(Arg.Any<CancellationToken>())
			.Returns<ValueTask<bool>>(_ => throw new InvalidOperationException("driver init failed"));

		BackendSelection selection = await CreateSelector().SelectBackendAsync(CancellationToken.None);

		selection.Backend.Should().Be(ComputeBackend.Cpu);
		selection.Reason.Should().Contain("CPU");
	}

	[Fact]
	public async Task Propagates_cancellation_rather_than_swallowing_it()
	{
		_probe.IsGpuRuntimeAvailableAsync(Arg.Any<CancellationToken>())
			.Returns<ValueTask<bool>>(_ => throw new OperationCanceledException());

		Func<Task> act = async () => await CreateSelector().SelectBackendAsync(new CancellationToken(canceled: true));

		await act.Should().ThrowAsync<OperationCanceledException>();
	}
}
