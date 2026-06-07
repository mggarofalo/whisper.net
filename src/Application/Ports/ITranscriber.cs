// Port for turning audio into text. Implemented in Infrastructure by the Whisper.net adapter; faked
// in the BDD specs so behavior can be driven without a real model.

using Domain.Audio;

namespace Application.Ports;

public interface ITranscriber
{
	ValueTask<TranscriptionResult> TranscribeAsync(AudioClip clip, CancellationToken cancellationToken);
}
