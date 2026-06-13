// Unit test for the user-data purge: the handler clears both local stores — transcript
// history and the audit log — through their ports. Uses substituted ports.

using Application.Ports;
using Application.Privacy;
using NSubstitute;
using Xunit;

namespace Application.Tests.Privacy;

public sealed class PurgeUserDataTests
{
	[Fact]
	public async Task Handler_clears_both_history_and_the_audit_log()
	{
		IHistoryStore history = Substitute.For<IHistoryStore>();
		IAuditLog auditLog = Substitute.For<IAuditLog>();
		PurgeUserDataHandler handler = new(history, auditLog);

		await handler.Handle(new PurgeUserDataCommand(), CancellationToken.None);

		await history.Received(1).ClearAsync(Arg.Any<CancellationToken>());
		await auditLog.Received(1).ClearAsync(Arg.Any<CancellationToken>());
	}
}
