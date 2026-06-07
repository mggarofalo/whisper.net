// Applies a named output transform (WHISPER-37): resolve the transform from the registry, compose its
// prompt with the input text, and delegate the rewrite to the rephrase port. Every non-success path is
// recoverable — an unknown name, a disabled backend, or a rephrase failure each return a TransformResult
// carrying the original text rather than throwing, so a transform problem never crashes the pipeline.
// The AI execution stays entirely behind IRephraseClient, so this framework holds no Infrastructure or
// network concern.

using Application.Ports;
using Application.Rephrase;

namespace Logic.AppManagement.OutputTransforms;

public sealed class OutputTransformService(OutputTransformRegistry registry, IRephraseClient rephraseClient)
{
	public async ValueTask<TransformResult> ApplyAsync(string transformName, string text, CancellationToken cancellationToken)
	{
		if (!registry.TryResolve(transformName, out OutputTransform transform))
		{
			// Unknown transform: recoverable, and no rephrase call is made.
			return TransformResult.UnknownTransform(text);
		}

		RephraseResult rephrase = await rephraseClient
			.RephraseAsync(text, transform.Prompt, cancellationToken)
			.ConfigureAwait(false);

		return rephrase.Status switch
		{
			RephraseStatus.Rephrased => TransformResult.Applied(rephrase.Text),
			RephraseStatus.Disabled => TransformResult.Disabled(rephrase.Text),
			_ => TransformResult.Failed(rephrase.Text),
		};
	}
}
