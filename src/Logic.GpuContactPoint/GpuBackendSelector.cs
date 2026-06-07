// The single, deliberate place that decides GPU-vs-CPU for the whole app — the "GPU contact point."
// It asks the raw Vulkan probe (IGpuProbe, Infrastructure) whether a usable runtime is present and
// turns that into a reportable BackendSelection: Vulkan when available, CPU otherwise. A probe that
// fails or throws is treated as "no GPU": the app must never hang or crash on a misconfigured driver,
// only fall back to CPU. Cancellation is cooperative and propagates.

using Application.Ports;
using Domain.Models;

namespace Logic.GpuContactPoint;

public sealed class GpuBackendSelector(IGpuProbe probe) : IBackendSelector
{
	public async ValueTask<BackendSelection> SelectBackendAsync(CancellationToken cancellationToken)
	{
		try
		{
			bool available = await probe.IsGpuRuntimeAvailableAsync(cancellationToken).ConfigureAwait(false);

			return available
				? new BackendSelection(ComputeBackend.Vulkan, "A usable Vulkan runtime is available.")
				: new BackendSelection(ComputeBackend.Cpu, "No usable Vulkan runtime is available; using the CPU backend.");
		}
		catch (OperationCanceledException)
		{
			throw;
		}
		catch (Exception ex)
		{
			// A failing or hanging probe must not take the app down — fall back to CPU and say why.
			return new BackendSelection(ComputeBackend.Cpu, $"Vulkan probe failed ({ex.GetType().Name}); using the CPU backend.");
		}
	}
}
