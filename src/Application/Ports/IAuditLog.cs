// Port for the opt-in audit log. Implemented in Infrastructure (the SQLite store); faked in
// specs that do not exercise the real store. The gating policy (off by default, written only when the
// user has enabled auditing) lives in Logic, not here — this port only appends, counts, and clears.
// Local-only by design: no method sends anything off the device.

using Domain.Audit;

namespace Application.Ports;

public interface IAuditLog
{
	/// <summary>Appends an audit record to the local audit log.</summary>
	ValueTask AppendAsync(AuditRecord record, CancellationToken cancellationToken);

	/// <summary>Returns how many audit records are currently stored.</summary>
	ValueTask<int> CountAsync(CancellationToken cancellationToken);

	/// <summary>Removes every audit record from disk (used by the user-initiated purge).</summary>
	ValueTask ClearAsync(CancellationToken cancellationToken);
}
