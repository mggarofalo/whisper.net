// The real Whisper.net engine — one of the two classes (with WhisperNetEngineFactory) that actually
// reference the Whisper.net package. It holds a loaded model (WhisperFactory) and, per call, builds a
// processor for the configured language and streams the model's segments. The model is assumed to be
// 16 kHz mono float PCM, as Whisper.net requires; the sample rate is accepted for symmetry with the
// seam but the model fixes the rate. Disposing releases the native model handle.

using System.Runtime.CompilerServices;
using Domain.Models;
using Whisper.net;

namespace Infrastructure.Transcription;

internal sealed class WhisperNetEngine(WhisperFactory factory, string? language) : IWhisperEngine
{
	public async IAsyncEnumerable<WhisperSegment> TranscribeAsync(
		IReadOnlyList<float> samples,
		int sampleRate,
		DecodingOptions decodingOptions,
		[EnumeratorCancellation] CancellationToken cancellationToken)
	{
		float[] buffer = samples as float[] ?? [.. samples];

		WhisperProcessorBuilder builder = factory.CreateBuilder();
		builder = IsAutoDetect(language) ? builder.WithLanguageDetection() : builder.WithLanguage(language!);

		// Custom-vocabulary conditioning (WHISPER-38): bias the decoder toward the user's terms via the
		// initial prompt, and — when a prompt is supplied — disable the first-token log-probability
		// threshold by pushing it to its floor, so the injected prompt cannot drop the genuine first token.
		if (decodingOptions.InitialPrompt is not null)
		{
			builder = builder.WithPrompt(decodingOptions.InitialPrompt);
		}

		if (decodingOptions.DisableFirstTokenLogProbThreshold)
		{
			builder = builder.WithLogProbThreshold(float.MinValue);
		}

		await using WhisperProcessor processor = builder.Build();

		await foreach (SegmentData segment in processor.ProcessAsync(buffer, cancellationToken).ConfigureAwait(false))
		{
			yield return new WhisperSegment(segment.Text, segment.Start, segment.End, segment.Probability);
		}
	}

	private static bool IsAutoDetect(string? language) =>
		string.IsNullOrWhiteSpace(language) || language.Equals("auto", StringComparison.OrdinalIgnoreCase);

	public ValueTask DisposeAsync()
	{
		factory.Dispose();
		return ValueTask.CompletedTask;
	}
}
