// One self-diagnostic check: a named probe of a single subsystem (audio capture, model
// cache, hotkey registration, GPU backend) that runs through the existing Application ports and reports
// a structured result. The contract lives here in Application so the doctor handler can aggregate an
// arbitrary set of checks; the concrete checks live in Logic.AppManagement, where the real subsystem
// knowledge belongs. A check reports an expected unavailability as a Fail/Warn result rather than
// throwing — the aggregator isolates unexpected exceptions so one check can never stop the others.

namespace Application.Diagnostics;

public interface IDiagnosticCheck
{
	/// <summary>The subsystem name shown in the report (e.g. "Audio", "Model", "Hotkey", "GPU").</summary>
	string Name { get; }

	/// <summary>Runs the check and reports its structured result.</summary>
	ValueTask<DiagnosticResult> RunAsync(CancellationToken cancellationToken);
}
