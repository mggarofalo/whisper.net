// Port for turning audio into text. Implemented in Infrastructure by the Whisper.net adapter; faked
// in the BDD specs so behavior can be driven without a real model.

using Domain.Audio;

namespace Application.Ports;

/// <summary>
/// Transcribes a captured audio clip into recognized text.
/// </summary>
/// <remarks>I/O/compute-bound (runs the model); async and cancellable, call off the UI thread.</remarks>
public interface ITranscriber
{
	/// <summary>Transcribes <paramref name="clip"/> and returns the recognized text.</summary>
	ValueTask<TranscriptionResult> TranscribeAsync(AudioClip clip, CancellationToken cancellationToken);
}
