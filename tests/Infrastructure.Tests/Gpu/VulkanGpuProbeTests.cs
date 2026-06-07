// Smoke coverage for the WHISPER-9 Vulkan probe. The probe's job is to answer "is a usable Vulkan
// runtime present?" promptly and WITHOUT crashing on any machine — present or absent, GPU box or
// headless CI agent. These tests assert exactly that contract: it returns a bool, never throws, and
// honors cancellation. Whether the answer is true or false depends on the host and is not asserted.

using AwesomeAssertions;
using Infrastructure.Gpu;
using Xunit;

namespace Infrastructure.Tests.Gpu;

public sealed class VulkanGpuProbeTests
{
	private readonly VulkanGpuProbe _probe = new();

	[Fact]
	public async Task Reports_availability_without_throwing()
	{
		Func<Task> act = async () => await _probe.IsGpuRuntimeAvailableAsync(CancellationToken.None);

		await act.Should().NotThrowAsync();
	}

	[Fact]
	public async Task Honors_cancellation()
	{
		Func<Task> act = async () => await _probe.IsGpuRuntimeAvailableAsync(new CancellationToken(canceled: true));

		await act.Should().ThrowAsync<OperationCanceledException>();
	}
}
