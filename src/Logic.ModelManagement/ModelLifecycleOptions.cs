// Lifecycle policy configuration: the compute precision to load models at, whether to warm a model up
// after loading (on by default, so the first utterance is not slow), and the transcription language.
// Applied at load time.

using Domain.Models;

namespace Logic.ModelManagement;

public sealed class ModelLifecycleOptions
{
	public const string SectionName = "ModelLifecycle";

	/// <summary>Numeric precision applied at load, consistent with the selected backend.</summary>
	public ComputePrecision Precision { get; set; } = ComputePrecision.Float16;

	/// <summary>Run a tiny inference after load so the first real transcription is not penalized.</summary>
	public bool WarmUp { get; set; } = true;

	/// <summary>Language code (empty/"auto" auto-detects).</summary>
	public string Language { get; set; } = "auto";
}
