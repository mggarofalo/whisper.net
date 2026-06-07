// The ordered post-process pipeline (WHISPER-41) behind the IPostProcessor port. It reads the live
// PostProcessSettingsHolder on every call, so a configuration edit takes effect on the next
// transcription without restarting the app. Step order (each independently toggleable):
//   1. Normalize  - strip noise labels (always) and, when enabled, filler words (WHISPER-36).
//   2. (Custom-vocabulary decode conditioning is applied UPSTREAM during transcription, WHISPER-38.)
//   3. Transform  - when a default transform is configured, rewrite via the transforms framework
//                   (WHISPER-37). An unknown/disabled/failed transform leaves the normalized text
//                   unchanged, so an invalid configuration degrades safely rather than crashing.

using Application.Configuration;
using Application.Ports;
using Logic.AppManagement.OutputTransforms;

namespace Logic.AppManagement.PostProcessing;

public sealed class PostProcessPipeline(
	IFillerWordCleaner cleaner,
	OutputTransformService transforms,
	PostProcessSettingsHolder holder) : IPostProcessor
{
	public async ValueTask<string> ProcessAsync(string text, CancellationToken cancellationToken)
	{
		PostProcessOptions options = holder.Current;

		string normalized = cleaner.Clean(text, options.RemoveFillerWords);

		if (string.IsNullOrWhiteSpace(options.DefaultTransform))
		{
			return normalized;
		}

		TransformResult transformed = await transforms
			.ApplyAsync(options.DefaultTransform, normalized, cancellationToken)
			.ConfigureAwait(false);

		// On Applied the rewritten text; on Disabled/Failed/UnknownTransform the original normalized text.
		return transformed.Text;
	}
}
