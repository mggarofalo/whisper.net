// Where a model is in its lifecycle, surfaced so the UI can show the user what's happening: nothing
// loaded, a load/switch in flight, or a model ready to transcribe.

namespace Domain.Models;

public enum ModelState
{
	Unloaded,
	Loading,
	Ready,
}
