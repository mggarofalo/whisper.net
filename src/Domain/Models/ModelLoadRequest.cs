// Everything the runtime needs to load a model: which model, where its file is, and the backend and
// precision to load it under (both decided up front so loading is deterministic). A value object passed
// from the lifecycle policy (Logic) down to the native runtime (Infrastructure).

namespace Domain.Models;

public sealed record ModelLoadRequest(
	string ModelId,
	string ModelPath,
	ComputeBackend Backend,
	ComputePrecision Precision,
	string Language);
