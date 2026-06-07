// A completed transcription as it is recorded in history: the recognized text, when it happened, and
// the word count derived from the text (the unit usage statistics aggregate over). A transcript
// entry with empty text is meaningless, so that is rejected at construction. Modeled as a record so
// it round-trips structurally to and from its DTO (the Mapperly mappers and history store rely on
// this).

namespace Domain.History;

public sealed record TranscriptEntry
{
	public Guid Id { get; }
	public string Text { get; }
	public DateTimeOffset CreatedAt { get; }
	public int WordCount { get; }

	public TranscriptEntry(Guid id, string text, DateTimeOffset createdAt)
	{
		if (string.IsNullOrWhiteSpace(text))
		{
			throw new DomainException("A transcript entry must have non-empty text.");
		}

		Id = id;
		Text = text;
		CreatedAt = createdAt;
		WordCount = CountWords(text);
	}

	// Records a new entry, assigning a fresh identity unless the caller supplies one.
	public static TranscriptEntry Create(string text, DateTimeOffset createdAt, Guid? id = null) =>
		new(id ?? Guid.NewGuid(), text, createdAt);

	private static int CountWords(string text) =>
		text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length;
}
