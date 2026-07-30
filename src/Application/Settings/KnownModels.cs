// The set of Whisper model ids the settings validator accepts before a selection is persisted. The
// model registry (Logic.ModelManagement) owns download/cache state, sizes, and GPU suitability; this
// stays the validator's allow-list of recognized ids.

namespace Application.Settings;

public static class KnownModels
{
	private static readonly HashSet<string> Ids = new(StringComparer.OrdinalIgnoreCase)
	{
		"tiny", "tiny.en",
		"base", "base.en",
		"small", "small.en",
		"medium", "medium.en",
		"large-v1", "large-v2", "large-v3",
		"large-v3-turbo", "large-v3-turbo-q5_0",
	};

	public static bool IsKnown(string modelId) => !string.IsNullOrWhiteSpace(modelId) && Ids.Contains(modelId);
}
