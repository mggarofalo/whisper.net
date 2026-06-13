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

	/// <summary>
	/// Loads the active model and runs a tiny warm-up inference so the FIRST real dictation is not
	/// penalized by the cold model load and lazy native initialization. Best-effort and
	/// idempotent: if there is no usable model yet, the implementation surfaces the same typed error
	/// <see cref="TranscribeAsync"/> would, for the caller to swallow. Safe to call before any real clip.
	/// </summary>
	ValueTask PreloadAsync(CancellationToken cancellationToken);
}
