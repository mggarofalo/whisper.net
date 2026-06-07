// Detects whether a usable Vulkan runtime is present WITHOUT initializing a device — initializing a
// real device is exactly what could hang on a broken driver (the failure mode the Python app hit with
// CUDA). It only asks the OS loader to resolve the Vulkan loader library ("vulkan-1"): if it loads, a
// Vulkan runtime is installed and the GPU contact point may select it; if not, the app falls back to
// CPU. This is the single place that touches the native Vulkan loader. Detection is purely local — no
// network egress.

using System.Runtime.InteropServices;
using Application.Ports;

namespace Infrastructure.Gpu;

public sealed class VulkanGpuProbe : IGpuProbe
{
	// The platform-specific name of the Vulkan loader. On Windows it resolves "vulkan-1.dll"; on Linux
	// the loader maps the bare name to "libvulkan.so.1" via the usual search.
	private const string VulkanLoader = "vulkan-1";

	public ValueTask<bool> IsGpuRuntimeAvailableAsync(CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();

		bool available = NativeLibrary.TryLoad(VulkanLoader, out nint handle);
		if (available)
		{
			NativeLibrary.Free(handle);
		}

		return ValueTask.FromResult(available);
	}
}
