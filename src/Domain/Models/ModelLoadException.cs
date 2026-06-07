// The typed error raised when a model file exists but cannot be loaded — corrupt, truncated, or an
// incompatible format. Like ModelNotFoundException it converts a native failure into a typed,
// catchable signal so the app degrades gracefully instead of crashing.

namespace Domain.Models;

public sealed class ModelLoadException : Exception
{
	public ModelLoadException(string modelPath, Exception innerException)
		: base($"The model file at '{modelPath}' could not be loaded.", innerException) => ModelPath = modelPath;

	/// <summary>The path of the model file that failed to load.</summary>
	public string ModelPath { get; }
}
