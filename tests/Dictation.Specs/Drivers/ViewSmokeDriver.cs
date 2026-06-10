// Exercises the @WHISPER-96 presentation-test-strategy requirement. The smoke tests themselves are
// WPF (net10.0-windows) and run in their own project inside the same gate; this spec — like the
// packaging/guidance/theming drivers — inspects the repository artifacts to pin the strategy: the STA
// harness exists and covers construction + first bind with binding-trace errors failing, the
// DataTemplate completeness check enumerates the real registered sections, the FlaUI adopt-vs-defer
// decision is recorded with rationale, and CI's Windows test step gates the smoke project with the
// rest of the solution.

using AwesomeAssertions;

namespace Dictation.Specs.Drivers;

public sealed class ViewSmokeDriver
{
	private static readonly string RepositoryRoot = FindRepositoryRoot();

	private static string SmokeProjectRoot => Path.Combine(RepositoryRoot, "tests", "Presentation.Smoke.Tests");

	public void AssertStaHarnessConstructsEachFeatureView()
	{
		string project = Path.Combine(SmokeProjectRoot, "Presentation.Smoke.Tests.csproj");
		File.Exists(project).Should().BeTrue("the smoke project must exist (WHISPER-96 AC1)");

		string csproj = File.ReadAllText(project);
		csproj.Should().Contain("net10.0-windows", "the smoke layer is WPF and Windows-only");
		csproj.Should().Contain("Presentation.csproj", "the smoke layer tests the real views");

		string sta = File.ReadAllText(Path.Combine(SmokeProjectRoot, "StaThread.cs"));
		sta.Should().Contain("ApartmentState.STA", "WPF elements demand an STA thread");

		string tests = File.ReadAllText(Path.Combine(SmokeProjectRoot, "FeatureViewSmokeTests.cs"));
		tests.Should().Contain("Each_feature_view_constructs_and_binds_without_errors",
			"a dedicated test covers construction + first bind");
		tests.Should().Contain("host.Content = scope.Get(section.ViewModelType)",
			"each view is instantiated by implicit template resolution against its real, scope-resolved view-model");
	}

	public void AssertHarnessFailsOnBindingErrors()
	{
		string collector = File.ReadAllText(Path.Combine(SmokeProjectRoot, "BindingErrorCollector.cs"));
		collector.Should().Contain("PresentationTraceSources", "binding failures only surface as trace events");

		string tests = File.ReadAllText(Path.Combine(SmokeProjectRoot, "FeatureViewSmokeTests.cs"));
		tests.Should().Contain("BindingErrorCollector", "the harness collects binding errors during the first bind");
		tests.Should().Contain("Errors.Should().BeEmpty", "a collected binding error fails the test (WHISPER-96 AC1)");
	}

	public void AssertTemplateCompletenessChecked()
	{
		string tests = File.ReadAllText(Path.Combine(SmokeProjectRoot, "FeatureViewSmokeTests.cs"));

		tests.Should().Contain("NavigationSection", "the completeness check enumerates the real registered sections");
		tests.Should().Contain("DataTemplateKey", "each section's view-model type must key an implicit template");
		tests.Should().Contain("Every_registered_section_has_a_matching_data_template",
			"a missing template fails a dedicated test (WHISPER-96 AC2)");
	}

	public void AssertFlauiDecisionRecorded()
	{
		string doc = File.ReadAllText(Path.Combine(RepositoryRoot, "docs", "bdd-strategy.md"));

		doc.Should().Contain("FlaUI", "the adopt-vs-defer decision names the candidate framework (WHISPER-96 AC3)");
		doc.Should().Contain("defer FlaUI adoption", "the decision is explicit");
		doc.Should().Contain("revisit when the UI grows", "the decision records when to revisit, not just a verdict");
	}

	public void AssertCiRunsSmokeLayerOnWindows()
	{
		string solution = File.ReadAllText(Path.Combine(RepositoryRoot, "Whisper.slnx"));
		solution.Should().Contain("Presentation.Smoke.Tests", "the smoke project is part of the gated solution");

		string ci = File.ReadAllText(Path.Combine(RepositoryRoot, ".github", "workflows", "ci.yml"));
		ci.Should().Contain("windows-latest", "the test gate runs on Windows (WPF)");
		ci.Should().Contain("dotnet test Whisper.slnx", "the gate tests the whole solution, smoke layer included");
	}

	private static string FindRepositoryRoot()
	{
		DirectoryInfo? directory = new(AppContext.BaseDirectory);
		while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Whisper.slnx")))
		{
			directory = directory.Parent;
		}

		return directory?.FullName
			?? throw new InvalidOperationException("Could not locate the repository root (Whisper.slnx).");
	}
}
