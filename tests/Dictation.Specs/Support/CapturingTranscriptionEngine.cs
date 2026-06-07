// A fake engine seam for the @WHISPER-38 transcription scenario: it lets the REAL WhisperTranscriber
// run without a model or the native library, while recording the per-call DecodingOptions the decoder
// was conditioned with and how many times an engine was created. That lets a scenario prove a changed
// vocabulary conditions the next transcription with no reload (CreateCount stays at one).

using System.Runtime.CompilerServices;
using Domain.Models;
using Infrastructure.Transcription;

namespace Dictation.Specs.Support;

internal sealed class CapturingTranscriptionEngineFactory : IWhisperEngineFactory
{
	public int CreateCount { get; private set; }

	public DecodingOptions? LastDecodingOptions { get; private set; }

	public IWhisperEngine Create(string modelPath, ComputeBackend backend, string? language)
	{
		CreateCount++;
		return new CapturingEngine(this);
	}

	private sealed class CapturingEngine(CapturingTranscriptionEngineFactory owner) : IWhisperEngine
	{
		public async IAsyncEnumerable<WhisperSegment> TranscribeAsync(
			IReadOnlyList<float> samples,
			int sampleRate,
			DecodingOptions decodingOptions,
			[EnumeratorCancellation] CancellationToken cancellationToken)
		{
			owner.LastDecodingOptions = decodingOptions;
			cancellationToken.ThrowIfCancellationRequested();
			yield return new WhisperSegment("ok", TimeSpan.Zero, TimeSpan.FromSeconds(1), 1f);
			await Task.CompletedTask;
		}

		public ValueTask DisposeAsync() => ValueTask.CompletedTask;
	}
}
