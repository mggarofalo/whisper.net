// The privacy gate for the opt-in audit log. It reads the LIVE settings holder on every
// call, so enabling or disabling auditing takes effect immediately, without an app restart: when
// AuditLogEnabled is off (the default), nothing is written; when on, the record is appended to the local
// audit log via the port. Keeping the gate here (Logic) keeps the policy out of the Infrastructure store,
// which only knows how to persist what it is given.

using Application.Ports;
using Domain.Audit;
using Logic.AppManagement.Settings;

namespace Logic.AppManagement.Audit;

public sealed class AuditLogger(SettingsHolder settings, IAuditLog auditLog)
{
	public async ValueTask RecordAsync(AuditRecord record, CancellationToken cancellationToken)
	{
		if (!settings.Current.AuditLogEnabled)
		{
			// Auditing is off (the privacy default): produce no record at all.
			return;
		}

		await auditLog.AppendAsync(record, cancellationToken);
	}
}
