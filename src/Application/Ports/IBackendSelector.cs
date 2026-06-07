// Port for the one decision the app makes about GPU vs CPU: which compute backend transcription runs
// on. The policy lives in Logic.GpuContactPoint (the single GPU touch point); the raw Vulkan-runtime
// detection it consults is IGpuProbe, implemented in Infrastructure. Faked in specs so CPU-fallback
// behavior can be driven without real hardware.

using Domain.Models;

namespace Application.Ports;

/// <summary>
/// Selects the compute backend transcription should run on, falling back to CPU when no usable GPU
/// runtime is present. This is the single seam the model layer asks "GPU or CPU, and why?".
/// </summary>
public interface IBackendSelector
{
	/// <summary>Selects the compute backend, reporting the choice and the reason for it.</summary>
	ValueTask<BackendSelection> SelectBackendAsync(CancellationToken cancellationToken);
}
