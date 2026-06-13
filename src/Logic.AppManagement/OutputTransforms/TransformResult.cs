// The outcome of applying an output transform. Carries the resulting Text plus a Status
// the caller can branch on, so every non-success path is recoverable rather than an exception:
//   Applied          - the rephrase model rewrote the text.
//   Disabled         - AI rephrase is off/unavailable; Text is the original, unchanged (graceful).
//   Failed           - the rephrase backend errored; Text is the original, unchanged (graceful).
//   UnknownTransform - no transform is registered under the requested name; Text is the original.

namespace Logic.AppManagement.OutputTransforms;

public enum TransformStatus
{
	Applied,
	Disabled,
	Failed,
	UnknownTransform,
}

public sealed record TransformResult(TransformStatus Status, string Text)
{
	public static TransformResult Applied(string text) => new(TransformStatus.Applied, text);

	public static TransformResult Disabled(string text) => new(TransformStatus.Disabled, text);

	public static TransformResult Failed(string text) => new(TransformStatus.Failed, text);

	public static TransformResult UnknownTransform(string text) => new(TransformStatus.UnknownTransform, text);
}
