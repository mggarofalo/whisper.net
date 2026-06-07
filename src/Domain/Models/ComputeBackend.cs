// The compute backend transcription runs on. Vulkan is the GPU path (the app deliberately avoids
// CUDA to sidestep toolkit-version hangs); Cpu is the always-available fallback. The GPU contact
// point picks exactly one of these and reports why.

namespace Domain.Models;

public enum ComputeBackend
{
	Cpu,
	Vulkan,
}
