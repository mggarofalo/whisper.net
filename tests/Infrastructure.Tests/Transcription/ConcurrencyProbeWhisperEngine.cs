// A fake engine for the WHISPER-130 inference race: it parks inside an inference call until released and
// records the maximum number of inference calls observed running at once. The transcriber shares ONE engine
// between the startup warm-up and a real dictation, and whisper_full is not safe to run concurrently on a
// single context — so a test can use this probe to prove the transcriber serializes the two (it must never
// let a real dictation enter inference while the warm-up inference is still in flight).

using System.Runtime.CompilerServices;
using Domain.Models;
using Infrastructure.Transcription;

namespace Infrastructure.Tests.Transcription;

internal sealed class ConcurrencyProbeWhisperEngineFactory : IWhisperEngineFactory
{
	// One shared engine, exactly as the real WhisperTranscriber holds a single loaded engine across calls.
	public ConcurrencyProbeWhisperEngine Engine { get; } = new();

	public int CreateCount { get; private set; }

	public IWhisperEngine Create(string modelPath, ComputeBackend backend, string? language)
	{
		CreateCount++;
		return Engine;
	}
}

internal sealed class ConcurrencyProbeWhisperEngine : IWhisperEngine
{
	// Lets a parked inference call complete; one shared signal, so every call past this point runs to the end.
	private readonly TaskCompletionSource _release = new(TaskCreationOptions.RunContinuationsAsynchronously);

	// Completes the moment the FIRST inference call enters the engine, so a test can sequence the next call.
	private readonly TaskCompletionSource _firstEntered = new(TaskCreationOptions.RunContinuationsAsynchronously);

	private int _active;

	/// <summary>The high-water mark of inference calls running at once. Must stay 1 if the transcriber serializes.</summary>
	public int MaxConcurrent { get; private set; }

	/// <summary>Completes once an inference call has entered the engine.</summary>
	public Task FirstInferenceEntered => _firstEntered.Task;

	/// <summary>Releases the parked inference call(s) so they run to completion.</summary>
	public void Release() => _release.TrySetResult();

	public async IAsyncEnumerable<WhisperSegment> TranscribeAsync(
		IReadOnlyList<float> samples,
		int sampleRate,
		DecodingOptions decodingOptions,
		[EnumeratorCancellation] CancellationToken cancellationToken)
	{
		int active = Interlocked.Increment(ref _active);
		MaxConcurrent = Math.Max(MaxConcurrent, active);
		_firstEntered.TrySetResult();
		try
		{
			// Park inside inference until the test releases us, holding the call "in flight" so an unserialized
			// second call would be observed running concurrently (MaxConcurrent > 1).
			await _release.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
			yield return new WhisperSegment("hi", TimeSpan.Zero, TimeSpan.Zero, 1f);
		}
		finally
		{
			Interlocked.Decrement(ref _active);
		}
	}

	public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
