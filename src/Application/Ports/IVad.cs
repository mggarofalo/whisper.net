// Port for voice-activity detection over a captured clip. Implemented in Infrastructure by the Silero
// ONNX adapter (Module 2); faked in specs so silence/speech policy can be driven deterministically.

using Domain.Audio;

namespace Application.Ports;

/// <summary>
/// Detects whether captured audio contains speech, used to gate empty recordings and to find
/// trailing silence for trimming.
/// </summary>
/// <remarks>I/O-bound (runs an inference session); call off the UI thread and honor cancellation.</remarks>
public interface IVad
{
	/// <summary>Reports whether the clip contains speech rather than only silence or noise.</summary>
	ValueTask<bool> ContainsSpeechAsync(AudioClip clip, CancellationToken cancellationToken);
}
