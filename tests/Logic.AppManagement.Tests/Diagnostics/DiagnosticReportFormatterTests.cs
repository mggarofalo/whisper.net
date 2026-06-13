// Unit tests for the doctor report formatter: proves "prints a clear pass/warn/fail report"
// without a real console. Each check renders to a tagged line carrying its name and detail, and the
// report ends with an overall line that reflects the worst status.

using Application.Diagnostics;
using AwesomeAssertions;
using Logic.AppManagement.Diagnostics;
using Xunit;

namespace Logic.AppManagement.Tests.Diagnostics;

public sealed class DiagnosticReportFormatterTests
{
	[Fact]
	public void Renders_each_check_with_a_status_tag_name_and_detail()
	{
		DiagnosticReport report = new(
		[
			new DiagnosticResult("Audio", DiagnosticStatus.Pass, "2 capture device(s) available; default: Microphone."),
			new DiagnosticResult("GPU", DiagnosticStatus.Warn, "No usable Vulkan runtime is available; using the CPU backend."),
		]);

		string text = DiagnosticReportFormatter.Format(report);

		text.Should().Contain("[PASS] Audio — 2 capture device(s) available; default: Microphone.");
		text.Should().Contain("[WARN] GPU — No usable Vulkan runtime is available; using the CPU backend.");
	}

	[Fact]
	public void Ends_with_an_overall_line_reflecting_the_worst_status()
	{
		DiagnosticReport report = new(
		[
			new DiagnosticResult("Audio", DiagnosticStatus.Pass, "ok"),
			new DiagnosticResult("Model", DiagnosticStatus.Fail, "missing"),
		]);

		string text = DiagnosticReportFormatter.Format(report);

		text.Should().Contain("[FAIL] Model — missing");
		text.TrimEnd().Should().EndWith("Overall: FAIL");
	}
}
