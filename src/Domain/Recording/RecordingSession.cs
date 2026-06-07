// One push-to-talk recording from the moment capture starts until it stops. An entity: it has a
// stable identity (Id) and a lifecycle, unlike the value objects elsewhere in the domain. The core
// invariant is temporal — a session can never end before it started — enforced by the guarded End
// transition, the only way to set the end time.

namespace Domain.Recording;

public sealed class RecordingSession
{
	public Guid Id { get; }
	public DateTimeOffset StartedAt { get; }
	public DateTimeOffset? EndedAt { get; private set; }

	public bool IsComplete => EndedAt is not null;

	public TimeSpan? Duration => EndedAt is { } endedAt ? endedAt - StartedAt : null;

	private RecordingSession(Guid id, DateTimeOffset startedAt)
	{
		Id = id;
		StartedAt = startedAt;
	}

	// Begins a session at the given instant. A caller may supply an id (e.g. when rehydrating).
	public static RecordingSession Start(DateTimeOffset startedAt, Guid? id = null) =>
		new(id ?? Guid.NewGuid(), startedAt);

	// Ends the session, rejecting any end instant earlier than the start.
	public RecordingSession End(DateTimeOffset endedAt)
	{
		if (endedAt < StartedAt)
		{
			throw new DomainException("A recording session cannot end before it starts.");
		}

		EndedAt = endedAt;
		return this;
	}
}
