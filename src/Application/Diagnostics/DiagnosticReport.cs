// The aggregate result of running every diagnostic check (WHISPER-50). It carries the individual
// results in a stable order and derives a single Overall status as the worst of them, so a caller (the
// doctor command, an exit code) can answer "is anything broken?" without re-scanning the list. An empty
// report is treated as passing.

namespace Application.Diagnostics;

public sealed record DiagnosticReport(IReadOnlyList<DiagnosticResult> Results)
{
	/// <summary>The worst status across all checks — Fail if any failed, else Warn if any warned, else Pass.</summary>
	public DiagnosticStatus Overall =>
		Results.Count == 0 ? DiagnosticStatus.Pass : Results.Max(result => result.Status);
}
