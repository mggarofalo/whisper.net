// A single text post-processing rule: find some text in a transcription and replace it (e.g. expand
// "asap" to "as soon as possible", or map a spoken command to punctuation). Immutable; a rule must
// have a name and something to find. The transform engine that applies these arrives in Module 8 —
// this is the domain shape it operates over.

namespace Domain.Settings;

public sealed record TransformDefinition
{
	public string Name { get; }
	public string Find { get; }
	public string Replace { get; }
	public bool Enabled { get; }

	public TransformDefinition(string name, string find, string replace, bool enabled = true)
	{
		if (string.IsNullOrWhiteSpace(name))
		{
			throw new DomainException("Transform name must not be empty.");
		}

		if (string.IsNullOrEmpty(find))
		{
			throw new DomainException("Transform pattern to find must not be empty.");
		}

		Name = name;
		Find = find;
		Replace = replace ?? string.Empty;
		Enabled = enabled;
	}
}
