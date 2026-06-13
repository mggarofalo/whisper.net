// A single entry in the opt-in audit log: when an auditable event happened, what it was,
// and any richer detail (timing, source app, event traces). Privacy-sensitive data lives only here, on
// the local device, and only when the user has explicitly enabled auditing. An audit record with no
// event name is meaningless, so that is rejected at construction. Modeled as a record so it round-trips
// structurally to and from the audit store.

namespace Domain.Audit;

public sealed record AuditRecord
{
	public Guid Id { get; }
	public DateTimeOffset OccurredAt { get; }
	public string Event { get; }
	public string Detail { get; }

	public AuditRecord(Guid id, DateTimeOffset occurredAt, string @event, string detail)
	{
		if (string.IsNullOrWhiteSpace(@event))
		{
			throw new DomainException("An audit record must have a non-empty event.");
		}

		Id = id;
		OccurredAt = occurredAt;
		Event = @event;
		Detail = detail ?? string.Empty;
	}

	// Records a new audit entry, assigning a fresh identity unless the caller supplies one.
	public static AuditRecord Create(string @event, DateTimeOffset occurredAt, string detail = "", Guid? id = null) =>
		new(id ?? Guid.NewGuid(), occurredAt, @event, detail);
}
