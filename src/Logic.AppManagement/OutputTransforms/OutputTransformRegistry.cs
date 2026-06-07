// The catalog of named output transforms (WHISPER-37). Ships the built-in formats ported from
// whisper-local (bullets, prompt-engineer, polish) and resolves a transform by name (case-insensitive).
// Custom catalogs can be supplied for tests or future configuration; the default is the built-in set.

namespace Logic.AppManagement.OutputTransforms;

public sealed class OutputTransformRegistry
{
	private readonly Dictionary<string, OutputTransform> _transforms;

	public OutputTransformRegistry()
		: this(BuiltIns)
	{
	}

	public OutputTransformRegistry(IEnumerable<OutputTransform> transforms) =>
		_transforms = transforms.ToDictionary(transform => transform.Name, StringComparer.OrdinalIgnoreCase);

	/// <summary>The built-in transforms ported from whisper-local.</summary>
	public static IReadOnlyList<OutputTransform> BuiltIns { get; } =
	[
		new(
			"bullets",
			"Rewrite the text as a concise bulleted list.",
			"Convert the following text into a concise, well-organized bulleted list. "
				+ "Preserve all information; do not add commentary. Return only the bullet points.\n\n"),
		new(
			"prompt-engineer",
			"Rewrite the text as a clear, well-structured AI prompt.",
			"Rewrite the following into a clear, specific, well-structured prompt for an AI assistant. "
				+ "Keep the original intent; return only the rewritten prompt.\n\n"),
		new(
			"polish",
			"Fix grammar, spelling, and punctuation while preserving meaning and tone.",
			"Polish the following text: correct grammar, spelling, and punctuation while preserving the "
				+ "original meaning and tone. Return only the corrected text.\n\n"),
	];

	public bool TryResolve(string name, out OutputTransform transform)
	{
		if (!string.IsNullOrWhiteSpace(name) && _transforms.TryGetValue(name.Trim(), out OutputTransform? found))
		{
			transform = found;
			return true;
		}

		transform = null!;
		return false;
	}

	public IReadOnlyCollection<OutputTransform> All => _transforms.Values;
}
