// Infrastructure-internal seam isolating the Whisper.net native engine from the adapter's
// orchestration. WhisperTranscriber owns the device-independent logic (backend selection, segment
// mapping, cancellation, error typing) on top of this seam; only the real implementation
// (WhisperNetEngine / WhisperNetEngineFactory) touches the Whisper.net package. The seam exists so
// that orchestration can be driven in tests with a fake engine — no model file, no native library.

using Domain.Models;

namespace Infrastructure.Transcription;

/// <summary>A loaded model ready to transcribe 16 kHz mono float PCM into timed segments.</summary>
public interface IWhisperEngine : IAsyncDisposable
{
	/// <summary>Streams the recognized segments for <paramref name="samples"/> (mono float PCM at <paramref name="sampleRate"/>).</summary>
	IAsyncEnumerable<WhisperSegment> TranscribeAsync(IReadOnlyList<float> samples, int sampleRate, CancellationToken cancellationToken);
}

/// <summary>One segment as the engine reports it: text plus the timing/confidence Whisper.net exposes.</summary>
public sealed record WhisperSegment(string Text, TimeSpan Start, TimeSpan End, float Probability);

/// <summary>Loads a model from a local path onto the chosen backend, producing an <see cref="IWhisperEngine"/>.</summary>
public interface IWhisperEngineFactory
{
	/// <summary>Loads the model at <paramref name="modelPath"/> for <paramref name="backend"/>, transcribing in <paramref name="language"/> (null/"auto" auto-detects).</summary>
	IWhisperEngine Create(string modelPath, ComputeBackend backend, string? language);
}
