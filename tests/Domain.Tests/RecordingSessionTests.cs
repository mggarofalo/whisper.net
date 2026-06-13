// Covers the RecordingSession entity: valid lifecycle (start then end, with duration) and the
// temporal invariant that an end can never precede a start. The domain scenario exercises the
// rejection path; these tests add the happy path and identity behavior the scenario does not.

using AwesomeAssertions;
using Domain;
using Domain.Recording;
using Xunit;

namespace Domain.Tests;

public sealed class RecordingSessionTests
{
	private static readonly DateTimeOffset Start = new(2026, 1, 1, 10, 0, 0, TimeSpan.Zero);

	[Fact]
	public void Start_creates_an_open_session_with_an_identity()
	{
		RecordingSession session = RecordingSession.Start(Start);

		session.Id.Should().NotBe(Guid.Empty);
		session.StartedAt.Should().Be(Start);
		session.IsComplete.Should().BeFalse();
		session.Duration.Should().BeNull();
	}

	[Fact]
	public void End_after_start_completes_the_session_and_reports_duration()
	{
		RecordingSession session = RecordingSession.Start(Start);

		session.End(Start.AddSeconds(3));

		session.IsComplete.Should().BeTrue();
		session.EndedAt.Should().Be(Start.AddSeconds(3));
		session.Duration.Should().Be(TimeSpan.FromSeconds(3));
	}

	[Fact]
	public void End_before_start_is_rejected()
	{
		RecordingSession session = RecordingSession.Start(Start);

		Action ending = () => session.End(Start.AddSeconds(-1));

		ending.Should().Throw<DomainException>();
	}

	[Fact]
	public void End_exactly_at_start_is_allowed()
	{
		RecordingSession session = RecordingSession.Start(Start);

		session.End(Start);

		session.Duration.Should().Be(TimeSpan.Zero);
	}

	[Fact]
	public void Start_preserves_a_supplied_identity()
	{
		Guid id = Guid.NewGuid();

		RecordingSession session = RecordingSession.Start(Start, id);

		session.Id.Should().Be(id);
	}
}
