// Port for detecting GPU acceleration availability. Implemented via the Logic.GpuContactPoint / the
// Infrastructure GPU adapter (Module 3); faked in specs so CPU-fallback policy can be driven without
// real hardware. This is the single seam through which the app asks "is the GPU runtime available?".

namespace Application.Ports;

/// <summary>
/// Reports whether a compatible GPU runtime (e.g. Vulkan) is available to accelerate transcription,
/// so the model layer can fall back to CPU when it is not.
/// </summary>
/// <remarks>I/O-bound (may load native probe libraries); async and cancellable.</remarks>
public interface IGpuProbe
{
	/// <summary>Reports whether a compatible GPU runtime is available for transcription.</summary>
	ValueTask<bool> IsGpuRuntimeAvailableAsync(CancellationToken cancellationToken);
}
