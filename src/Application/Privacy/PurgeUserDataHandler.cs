// Handles PurgeUserDataCommand: clears both local stores — the transcript history and the audit log —
// through their ports. Pure orchestration; the stores own the actual deletion (and its fail-safe logging).

using Application.Interfaces;
using Application.Ports;

namespace Application.Privacy;

public sealed class PurgeUserDataHandler(IHistoryStore history, IAuditLog auditLog)
	: ICommandHandler<PurgeUserDataCommand, Mediator.Unit>
{
	public async ValueTask<Mediator.Unit> Handle(PurgeUserDataCommand command, CancellationToken cancellationToken)
	{
		await history.ClearAsync(cancellationToken);
		await auditLog.ClearAsync(cancellationToken);
		return Mediator.Unit.Value;
	}
}
