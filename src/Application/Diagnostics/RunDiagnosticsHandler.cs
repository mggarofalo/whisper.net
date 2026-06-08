// Handles RunDiagnosticsQuery (WHISPER-50): runs every registered IDiagnosticCheck in registration order
// and collects their results into a DiagnosticReport. The aggregation owns one guarantee the individual
// checks do not: isolation. Each check is awaited inside a try/catch so that a check which throws an
// unexpected exception is recorded as a Fail and the remaining checks still run to completion — a doctor
// command must always produce a full report, never abort halfway. Cancellation is honored and propagates.

using Application.Interfaces;

namespace Application.Diagnostics;

public sealed class RunDiagnosticsHandler(IEnumerable<IDiagnosticCheck> checks)
	: IQueryHandler<RunDiagnosticsQuery, DiagnosticReport>
{
	public async ValueTask<DiagnosticReport> Handle(RunDiagnosticsQuery query, CancellationToken cancellationToken)
	{
		List<DiagnosticResult> results = [];

		foreach (IDiagnosticCheck check in checks)
		{
			cancellationToken.ThrowIfCancellationRequested();
			results.Add(await RunIsolatedAsync(check, cancellationToken));
		}

		return new DiagnosticReport(results);
	}

	// Runs a single check, turning an unexpected exception into a Fail result so the other checks still
	// run. A cancellation is not a diagnostic failure — it propagates so the whole run is abandoned.
	private static async ValueTask<DiagnosticResult> RunIsolatedAsync(IDiagnosticCheck check, CancellationToken cancellationToken)
	{
		try
		{
			return await check.RunAsync(cancellationToken);
		}
		catch (OperationCanceledException)
		{
			throw;
		}
		catch (Exception ex)
		{
			return new DiagnosticResult(check.Name, DiagnosticStatus.Fail, $"The {check.Name} check could not complete ({ex.GetType().Name}: {ex.Message}).");
		}
	}
}
