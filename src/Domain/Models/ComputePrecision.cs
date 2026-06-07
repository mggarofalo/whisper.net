// The numeric precision a model runs at, chosen against the selected backend and applied at load.
// Higher precision is more accurate but slower/heavier; quantized is smallest/fastest. The default
// (fp16) is the usual sweet spot for the ggml models the app ships.

namespace Domain.Models;

public enum ComputePrecision
{
	Float16,
	Float32,
	Quantized,
}
