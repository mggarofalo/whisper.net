// The native side of the model lifecycle, built on the Whisper.net engine seam. Loading creates an
// engine for the requested model/backend/language and wraps it in a
// handle the lifecycle policy can warm up, transcribe through, and release. (Whisper.net fixes a
// model's precision at the file/backend level rather than exposing a runtime knob, so the requested
// precision is decided and recorded by the policy at load time; there is no separate native toggle to
// set here.) Disposing a handle releases the underlying native model.

using System.Text;
using Application.Ports;
using Domain.Audio;
using Domain.Models;

namespace Infrastructure.Transcription;

public sealed class WhisperModelRuntime(IWhisperEngineFactory engineFactory) : IModelRuntime
{
	public ValueTask<IModelHandle> LoadAsync(ModelLoadRequest request, CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();
		IWhisperEngine engine = engineFactory.Create(request.ModelPath, request.Backend, request.Language);
		return ValueTask.FromResult<IModelHandle>(new WhisperModelHandle(engine));
	}

	private sealed class WhisperModelHandle(IWhisperEngine engine) : IModelHandle
	{
		// A tenth of a second of silence — enough to force shader compilation / model upload during
		// warmup so the first real utterance pays none of that lazy-initialization cost.
		private static readonly AudioClip WarmupClip = new(new float[1_600], 16_000);

		public async ValueTask WarmUpAsync(CancellationToken cancellationToken)
		{
			await foreach (WhisperSegment _ in engine
				.TranscribeAsync(WarmupClip.Samples, WarmupClip.SampleRate, DecodingOptions.Default, cancellationToken)
				.ConfigureAwait(false))
			{
				// Drain the warmup inference; its output is discarded.
			}
		}

		public async ValueTask<TranscriptionResult> TranscribeAsync(AudioClip clip, CancellationToken cancellationToken)
		{
			StringBuilder text = new();
			List<TranscriptionSegment> segments = [];

			await foreach (WhisperSegment segment in engine
				.TranscribeAsync(clip.Samples, clip.SampleRate, DecodingOptions.Default, cancellationToken)
				.ConfigureAwait(false))
			{
				text.Append(segment.Text);
				segments.Add(new TranscriptionSegment(segment.Text, segment.Start, segment.End, segment.Probability));
			}

			return new TranscriptionResult(text.ToString().Trim(), segments);
		}

		public ValueTask DisposeAsync() => engine.DisposeAsync();
	}
}
