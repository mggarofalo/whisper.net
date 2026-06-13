// CQRS query for the doctor / selftest command: runs every registered diagnostic check and
// returns the aggregated report. A read-only request carrying no data — the configured subsystems are
// read from the ports inside each check. Sent by the `--doctor` entry point (and any future tray action)
// so the diagnostics run through the same composition the rest of the app uses.

using Application.Interfaces;

namespace Application.Diagnostics;

public sealed record RunDiagnosticsQuery : IQuery<DiagnosticReport>;
