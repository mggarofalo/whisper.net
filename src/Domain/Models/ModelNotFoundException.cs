// The typed error raised when a transcription is requested against a model file that does not exist on
// disk. Surfacing this instead of letting the native loader crash lets the app fail gracefully (prompt
// the user, fall back, log) for a missing/invalid model.

namespace Domain.Models;

public sealed class ModelNotFoundException : Exception
{
	public ModelNotFoundException(string modelPath)
		: base($"No model file was found at '{modelPath}'.") => ModelPath = modelPath;

	/// <summary>The path that was expected to hold the model file.</summary>
	public string ModelPath { get; }
}
