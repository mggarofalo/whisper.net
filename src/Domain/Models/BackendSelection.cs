// The outcome of the GPU contact point's one decision: which compute backend transcription will run
// on, and why. Carrying the reason as a first-class value (not a buried log line) lets the UI and
// logs show the user whether the GPU was engaged or the app fell back to CPU.

namespace Domain.Models;

public sealed record BackendSelection(ComputeBackend Backend, string Reason)
{
	/// <summary>True when a GPU backend was selected; false when running on CPU.</summary>
	public bool IsGpu => Backend is not ComputeBackend.Cpu;
}
