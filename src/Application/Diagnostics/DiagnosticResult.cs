// The structured outcome of one diagnostic check: the subsystem's name, its status, and a
// human-readable detail explaining the verdict (which device, which model path, why the GPU fell back
// to CPU). Keeping the result structured — rather than a pre-formatted console line — is what lets the
// behavior be asserted in specs independent of how the doctor command prints it.

namespace Application.Diagnostics;

public sealed record DiagnosticResult(string Name, DiagnosticStatus Status, string Detail);
