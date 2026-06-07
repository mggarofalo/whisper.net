// A completed transcription as it is recorded in history: the recognized text, when it happened, how
// long the captured audio ran, and the word count derived from the text (the unit usage statistics
// aggregate over). A transcript entry with empty text is meaningless, and a negative audio duration is
// impossible, so both are rejected at construction. Modeled as a record so it round-trips structurally
// to and from its DTO (the Mapperly mappers and history store rely on this).

namespace Domain.History;

public sealed record TranscriptEntry
{
	public Guid Id { get; }
	public string Text { get; }
	public DateTimeOffset CreatedAt { get; }
	public TimeSpan AudioDuration { get; }
	public int WordCount { get; }

	public TranscriptEntry(Guid id, string text, DateTimeOffset createdAt, TimeSpan audioDuration = default)
	{
		if (string.IsNullOrWhiteSpace(text))
		{
			throw new DomainException("A transcript entry must have non-empty text.");
		}

		if (audioDuration < TimeSpan.Zero)
		{
			throw new DomainException("A transcript entry's audio duration must not be negative.");
		}

		Id = id;
		Text = text;
		CreatedAt = createdAt;
		AudioDuration = audioDuration;
		WordCount = CountWords(text);
	}

	// Records a new entry, assigning a fresh identity unless the caller supplies one.
	public static TranscriptEntry Create(string text, DateTimeOffset createdAt, TimeSpan audioDuration = default, Guid? id = null) =>
		new(id ?? Guid.NewGuid(), text, createdAt, audioDuration);

	private static int CountWords(string text) =>
		text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length;
}
