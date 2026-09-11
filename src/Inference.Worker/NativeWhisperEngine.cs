using System.Runtime.CompilerServices;
using Domain.Models;
using Whisper.net;
using Whisper.net.LibraryLoader;

namespace Inference.Worker;

internal sealed class NativeWhisperEngine : IAsyncDisposable
{
	private readonly WhisperFactory _factory;
	private readonly string? _language;

	public NativeWhisperEngine(string modelPath, ComputeBackend backend, string? language)
	{
		RuntimeOptions.RuntimeLibraryOrder = backend == ComputeBackend.Vulkan
			? [RuntimeLibrary.Vulkan, RuntimeLibrary.Cpu]
			: [RuntimeLibrary.Cpu];
		_factory = WhisperFactory.FromPath(modelPath);
		_language = language;
	}

	public async IAsyncEnumerable<NativeWhisperSegment> TranscribeAsync(
		IReadOnlyList<float> samples,
		DecodingOptions decodingOptions,
		[EnumeratorCancellation] CancellationToken cancellationToken)
	{
		float[] buffer = samples as float[] ?? [.. samples];
		WhisperProcessorBuilder builder = _factory.CreateBuilder();
		builder = IsAutoDetect(_language) ? builder.WithLanguageDetection() : builder.WithLanguage(_language!);

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
			yield return new NativeWhisperSegment(
				segment.Text,
				segment.Start,
				segment.End,
				segment.Probability);
		}
	}

	private static bool IsAutoDetect(string? language) =>
		string.IsNullOrWhiteSpace(language) || language.Equals("auto", StringComparison.OrdinalIgnoreCase);

	public ValueTask DisposeAsync()
	{
		_factory.Dispose();
		return ValueTask.CompletedTask;
	}
}

internal sealed record NativeWhisperSegment(
	string Text,
	TimeSpan Start,
	TimeSpan End,
	float Probability);
