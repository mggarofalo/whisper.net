// Covers the TranscriptEntry history record: its non-empty-text invariant, the word count it derives
// (the input to usage-stats aggregation), and its structural equality (which the Mapperly round-trip
// in WHISPER-49 depends on).

using AwesomeAssertions;
using Domain;
using Domain.History;
using Xunit;

namespace Domain.Tests;

public sealed class TranscriptEntryTests
{
	private static readonly DateTimeOffset When = new(2026, 1, 1, 10, 0, 0, TimeSpan.Zero);

	[Theory]
	[InlineData("")]
	[InlineData("   ")]
	[InlineData("\t")]
	public void Empty_or_whitespace_text_is_rejected(string text)
	{
		Action creating = () => TranscriptEntry.Create(text, When);

		creating.Should().Throw<DomainException>();
	}

	[Fact]
	public void Create_assigns_an_identity_and_keeps_the_text()
	{
		TranscriptEntry entry = TranscriptEntry.Create("take notes", When);

		entry.Id.Should().NotBe(Guid.Empty);
		entry.Text.Should().Be("take notes");
		entry.CreatedAt.Should().Be(When);
	}

	[Theory]
	[InlineData("hello", 1)]
	[InlineData("schedule the meeting", 3)]
	[InlineData("  spaced   out  words  ", 3)]
	public void Word_count_is_derived_from_the_text(string text, int expectedWords)
	{
		TranscriptEntry entry = TranscriptEntry.Create(text, When);

		entry.WordCount.Should().Be(expectedWords);
	}

	[Fact]
	public void Entries_with_the_same_values_are_structurally_equal()
	{
		Guid id = Guid.NewGuid();

		TranscriptEntry a = new(id, "hello world", When);
		TranscriptEntry b = new(id, "hello world", When);

		a.Should().Be(b);
	}
}
