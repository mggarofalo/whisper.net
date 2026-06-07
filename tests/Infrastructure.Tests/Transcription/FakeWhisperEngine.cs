// A fake engine seam for driving WhisperTranscriber's orchestration without a real model or the native
// Whisper.net library. The factory records how it was asked to load (path/backend/language) and hands
// back an engine that yields a canned set of segments and honors cancellation — exactly the seam the
// real WhisperNetEngineFactory implements.

using System.Runtime.CompilerServices;
using Domain.Models;
using Infrastructure.Transcription;

namespace Infrastructure.Tests.Transcription;

internal sealed class FakeWhisperEngineFactory : IWhisperEngineFactory
{
	private readonly WhisperSegment[] _segments;

	public FakeWhisperEngineFactory(params WhisperSegment[] segments) => _segments = segments;

	public int CreateCount { get; private set; }
	public string? LastModelPath { get; private set; }
	public ComputeBackend? LastBackend { get; private set; }
	public string? LastLanguage { get; private set; }

	public IWhisperEngine Create(string modelPath, ComputeBackend backend, string? language)
	{
		CreateCount++;
		LastModelPath = modelPath;
		LastBackend = backend;
		LastLanguage = language;
		return new FakeWhisperEngine(_segments);
	}
}

internal sealed class FakeWhisperEngine(WhisperSegment[] segments) : IWhisperEngine
{
	public async IAsyncEnumerable<WhisperSegment> TranscribeAsync(
		IReadOnlyList<float> samples,
		int sampleRate,
		[EnumeratorCancellation] CancellationToken cancellationToken)
	{
		foreach (WhisperSegment segment in segments)
		{
			cancellationToken.ThrowIfCancellationRequested();
			yield return segment;
			await Task.Yield();
		}
	}

	public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
