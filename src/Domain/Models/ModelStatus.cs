// The observable identity + state of the model lifecycle at a moment in time: which model (if any) is
// the subject and what state it is in. The UI binds to this to show "loading base…", "ready: small",
// or "no model loaded".

namespace Domain.Models;

public sealed record ModelStatus(string? ModelId, ModelState State)
{
	/// <summary>No model is loaded.</summary>
	public static ModelStatus Unloaded { get; } = new(null, ModelState.Unloaded);
}
