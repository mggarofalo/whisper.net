// The set of Whisper model ids the app recognizes today, used by the settings validator to reject an
// unknown model before it is persisted. This is a deliberately small placeholder: Module 3 introduces
// the real model registry (download state, sizes, GPU suitability) and will supersede this list.

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
	};

	public static bool IsKnown(string modelId) => !string.IsNullOrWhiteSpace(modelId) && Ids.Contains(modelId);
}
