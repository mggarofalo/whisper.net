// Covers the AuditRecord value object (WHISPER-34): a non-empty event is required, a null detail is
// normalized to empty, and Create assigns a fresh identity and carries the supplied values.

using AwesomeAssertions;
using Domain;
using Domain.Audit;
using Xunit;

namespace Domain.Tests;

public sealed class AuditRecordTests
{
	private static readonly DateTimeOffset When = new(2026, 1, 1, 9, 0, 0, TimeSpan.Zero);

	[Theory]
	[InlineData("")]
	[InlineData("   ")]
	public void An_empty_event_is_rejected(string @event)
	{
		Action creating = () => _ = AuditRecord.Create(@event, When);

		creating.Should().Throw<DomainException>();
	}

	[Fact]
	public void Create_assigns_an_identity_and_carries_the_values()
	{
		AuditRecord record = AuditRecord.Create("TranscriptionCompleted", When, "delivered to the focused field");

		record.Id.Should().NotBe(Guid.Empty);
		record.OccurredAt.Should().Be(When);
		record.Event.Should().Be("TranscriptionCompleted");
		record.Detail.Should().Be("delivered to the focused field");
	}
}
