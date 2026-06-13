// Inner TDD loop for the diagnostics aggregator. The handler runs every registered check in
// order and is the one place that guarantees isolation: a check that throws becomes a Fail result and the
// remaining checks still run, so the doctor always produces a full report. Also covers the report's
// derived Overall status (the worst of the results) and that cancellation propagates rather than being
// swallowed as a failure.

using Application.Diagnostics;
using Xunit;

namespace Application.Tests.Diagnostics;

public sealed class RunDiagnosticsHandlerTests
{
	// A trivial in-memory check so the aggregator can be tested without any ports.
	private sealed class StubCheck(string name, DiagnosticStatus status) : IDiagnosticCheck
	{
		public string Name => name;

		public ValueTask<DiagnosticResult> RunAsync(CancellationToken cancellationToken) =>
			ValueTask.FromResult(new DiagnosticResult(name, status, $"{name} detail"));
	}

	private sealed class ThrowingCheck(string name) : IDiagnosticCheck
	{
		public string Name => name;

		public ValueTask<DiagnosticResult> RunAsync(CancellationToken cancellationToken) =>
			throw new InvalidOperationException("probe blew up");
	}

	private static async ValueTask<DiagnosticReport> Run(params IDiagnosticCheck[] checks) =>
		await new RunDiagnosticsHandler(checks).Handle(new RunDiagnosticsQuery(), CancellationToken.None);

	[Fact]
	public async Task Runs_every_check_in_registration_order()
	{
		DiagnosticReport report = await Run(
			new StubCheck("Audio", DiagnosticStatus.Pass),
			new StubCheck("Model", DiagnosticStatus.Pass),
			new StubCheck("GPU", DiagnosticStatus.Warn));

		Assert.Equal(["Audio", "Model", "GPU"], report.Results.Select(r => r.Name));
	}

	[Fact]
	public async Task A_throwing_check_becomes_a_failure_and_the_others_still_run()
	{
		DiagnosticReport report = await Run(
			new StubCheck("Audio", DiagnosticStatus.Pass),
			new ThrowingCheck("Model"),
			new StubCheck("GPU", DiagnosticStatus.Pass));

		Assert.Equal(3, report.Results.Count);
		DiagnosticResult model = Assert.Single(report.Results, r => r.Name == "Model");
		Assert.Equal(DiagnosticStatus.Fail, model.Status);
		Assert.Contains("could not complete", model.Detail);
		Assert.All(report.Results.Where(r => r.Name != "Model"), r => Assert.Equal(DiagnosticStatus.Pass, r.Status));
	}

	[Fact]
	public async Task Overall_is_the_worst_status()
	{
		Assert.Equal(DiagnosticStatus.Pass, (await Run(
			new StubCheck("A", DiagnosticStatus.Pass),
			new StubCheck("B", DiagnosticStatus.Pass))).Overall);

		Assert.Equal(DiagnosticStatus.Warn, (await Run(
			new StubCheck("A", DiagnosticStatus.Pass),
			new StubCheck("B", DiagnosticStatus.Warn))).Overall);

		Assert.Equal(DiagnosticStatus.Fail, (await Run(
			new StubCheck("A", DiagnosticStatus.Warn),
			new StubCheck("B", DiagnosticStatus.Fail))).Overall);
	}

	[Fact]
	public void Overall_of_an_empty_report_is_pass()
	{
		Assert.Equal(DiagnosticStatus.Pass, new DiagnosticReport([]).Overall);
	}

	[Fact]
	public async Task Cancellation_propagates_rather_than_becoming_a_failure()
	{
		using CancellationTokenSource cts = new();
		cts.Cancel();

		await Assert.ThrowsAsync<OperationCanceledException>(async () =>
			await new RunDiagnosticsHandler([new StubCheck("Audio", DiagnosticStatus.Pass)])
				.Handle(new RunDiagnosticsQuery(), cts.Token));
	}
}
