// An immutable value-object description of a Whisper model the app can run: a stable identifier, a
// human-friendly name, and its on-disk size. As a value object it has no identity — two descriptors
// with the same fields are equal (structural equality, provided by the record). The richer model
// registry behavior arrives in Module 3; this is the shape the rest of the domain reasons about.

namespace Domain.Models;

public sealed record ModelDescriptor
{
	public string Id { get; }
	public string DisplayName { get; }
	public long SizeBytes { get; }

	public ModelDescriptor(string id, string displayName, long sizeBytes)
	{
		if (string.IsNullOrWhiteSpace(id))
		{
			throw new DomainException("Model id must not be empty.");
		}

		if (string.IsNullOrWhiteSpace(displayName))
		{
			throw new DomainException("Model display name must not be empty.");
		}

		if (sizeBytes < 0)
		{
			throw new DomainException("Model size must not be negative.");
		}

		Id = id;
		DisplayName = displayName;
		SizeBytes = sizeBytes;
	}
}
