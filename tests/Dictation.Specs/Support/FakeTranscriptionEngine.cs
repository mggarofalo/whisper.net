// A fake engine seam for the @WHISPER-3 scenarios: it lets the REAL WhisperTranscriber run end-to-end
// without a model file or the native Whisper.net library. The factory yields one segment with the
// configured text and, by construction, performs no network access — so the "no network egress"
// guarantee is observable (NetworkAccessed is always false).

using System.Runtime.CompilerServices;
using Domain.Models;
using Infrastructure.Transcription;

namespace Dictation.Specs.Support;

internal sealed class FakeTranscriptionEngineFactory(string text) : IWhisperEngineFactory
{
	/// <summary>Always false: a local model load never touches the network.</summary>
	public bool NetworkAccessed => false;

	public IWhisperEngine Create(string modelPath, ComputeBackend backend, string? language) =>
		new FakeEngine(text);

	private sealed class FakeEngine(string text) : IWhisperEngine
	{
		public async IAsyncEnumerable<WhisperSegment> TranscribeAsync(
			IReadOnlyList<float> samples,
			int sampleRate,
			[EnumeratorCancellation] CancellationToken cancellationToken)
		{
			cancellationToken.ThrowIfCancellationRequested();
			yield return new WhisperSegment(text, TimeSpan.Zero, TimeSpan.FromSeconds(1), 1f);
			await Task.CompletedTask;
		}

		public ValueTask DisposeAsync() => ValueTask.CompletedTask;
	}
}
