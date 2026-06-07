// Port for voice-activity detection over a captured clip. Implemented in Infrastructure by the Silero
// ONNX adapter (Module 2); faked in specs so silence/speech policy can be driven deterministically.

using Domain.Audio;

namespace Application.Ports;

/// <summary>
/// Scores a captured clip for speech, window by window. The raw per-window probabilities feed the
/// silence-gating and trimming policy (which owns the thresholds), so the detector stays a pure
/// measurement independent of how the result is interpreted.
/// </summary>
/// <remarks>I/O-bound (runs an inference session); call off the UI thread and honor cancellation.</remarks>
public interface IVad
{
	/// <summary>Returns a speech probability for each fixed-size window across the clip.</summary>
	ValueTask<VadAnalysis> AnalyzeAsync(AudioClip clip, CancellationToken cancellationToken);
}
