// The set of output-transform names the app recognizes today, used by the post-process configuration
// validator to reject an unknown default transform before it is applied. Mirrors the built-in transforms
// shipped by OutputTransformRegistry (Logic.AppManagement); kept here so the Application validator does
// not depend on Logic, exactly as KnownModels mirrors the model catalog.

namespace Application.PostProcessing;

public static class KnownTransforms
{
	private static readonly HashSet<string> Names =
		new(StringComparer.OrdinalIgnoreCase) { "bullets", "prompt-engineer", "polish" };

	public static bool IsKnown(string name) => !string.IsNullOrWhiteSpace(name) && Names.Contains(name);
}
