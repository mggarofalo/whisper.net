// Renders a DiagnosticReport as a plain-text, console-friendly report for the doctor command
//. Pure and WPF-free so the "prints a clear pass/warn/fail report" behavior is unit-tested
// independent of any real console: each check becomes a "[PASS|WARN|FAIL] Name — detail" line, followed
// by an overall line. The status tags are fixed-width so the columns line up in a terminal.

using System.Text;
using Application.Diagnostics;

namespace Logic.AppManagement.Diagnostics;

public static class DiagnosticReportFormatter
{
	/// <summary>Renders the report as newline-separated lines: a title, one line per check, then the overall status.</summary>
	public static string Format(DiagnosticReport report)
	{
		StringBuilder builder = new();
		builder.AppendLine("Whisper diagnostics");

		foreach (DiagnosticResult result in report.Results)
		{
			builder.AppendLine($"  [{Tag(result.Status)}] {result.Name} — {result.Detail}");
		}

		builder.Append($"Overall: {Tag(report.Overall)}");
		return builder.ToString();
	}

	// Fixed-width, upper-case tag so report columns align: PASS / WARN / FAIL.
	private static string Tag(DiagnosticStatus status) => status switch
	{
		DiagnosticStatus.Pass => "PASS",
		DiagnosticStatus.Warn => "WARN",
		DiagnosticStatus.Fail => "FAIL",
		_ => status.ToString().ToUpperInvariant(),
	};
}
