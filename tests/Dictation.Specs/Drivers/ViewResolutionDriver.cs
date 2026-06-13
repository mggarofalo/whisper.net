// Exercises the view-resolution convention. Like the packaging/guidance/theming drivers,
// this inspects repository artifacts directly (the convention is pure WPF Presentation): the
// architecture guide records the implicit-DataTemplate standard, every registered NavigationSection's
// view-model has a template mapping it to a view, feature-view code-behind is construction-only, and
// no view reacts to view-model changes by property-name string matching. The sections themselves are
// resolved from the real AddAppManagement registration, so the template check cannot drift from the
// production section list.

using System.Text.RegularExpressions;
using AwesomeAssertions;
using Logic.AppManagement.Shell;

namespace Dictation.Specs.Drivers;

public sealed partial class ViewResolutionDriver(IEnumerable<NavigationSection> sections)
{
	private static readonly string RepositoryRoot = FindRepositoryRoot();

	public void AssertConventionDocumented()
	{
		string doc = File.ReadAllText(Path.Combine(RepositoryRoot, "docs", "architecture.md"));

		doc.Should().Contain("View ↔ view-model resolution", "the convention has its own documented section");
		doc.Should().Contain("implicit `DataTemplate`", "views are resolved by implicit data templates keyed on the view-model type");
		doc.Should().Contain("supplied by the DI container", "view-models come from the container, not a locator");
		doc.Should().Contain("ViewModelLocator", "the rejected locator pattern is named explicitly");
		doc.Should().Contain("InitializeComponent`-only", "feature-view code-behind discipline is recorded");
	}

	public void AssertEverySectionHasTemplate()
	{
		string xaml = File.ReadAllText(Path.Combine(RepositoryRoot, "src", "Presentation", "Shell", "ShellWindow.xaml"));

		foreach (NavigationSection section in sections)
		{
			string viewModelName = section.ViewModelType.Name;
			string viewName = viewModelName.Replace("ViewModel", "View");

			xaml.Should().Contain($"{{x:Type vm:{viewModelName}}}",
				$"the '{section.Key}' section's view-model must be keyed by an implicit DataTemplate");
			xaml.Should().Contain($"<views:{viewName}",
				$"the '{section.Key}' section's template must resolve to its view by naming convention");
		}
	}

	public void AssertFeatureViewCodeBehindIsConstructionOnly()
	{
		string viewsRoot = Path.Combine(RepositoryRoot, "src", "Presentation", "Shell", "Views");
		string[] offenders = Directory
			.EnumerateFiles(viewsRoot, "*.xaml.cs", SearchOption.AllDirectories)
			.Where(file =>
			{
				string code = StripLineComments(File.ReadAllText(file));
				return code.Contains("+=") || MethodDeclarationPattern().IsMatch(code);
			})
			.ToArray();

		offenders.Should().BeEmpty("feature-view code-behind holds nothing beyond InitializeComponent");
	}

	public void AssertNoViewSwitchesOnPropertyNames()
	{
		string presentationRoot = Path.Combine(RepositoryRoot, "src", "Presentation");
		string[] offenders = Directory
			.EnumerateFiles(presentationRoot, "*.cs", SearchOption.AllDirectories)
			.Where(file => !file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}"))
			.Where(file =>
			{
				string code = StripLineComments(File.ReadAllText(file));
				return code.Contains("e.PropertyName") || code.Contains("PropertyChanged +=");
			})
			.ToArray();

		offenders.Should().BeEmpty("views react to view-model state through bindings, not property-name matching");
	}

	private static string StripLineComments(string code) =>
		string.Join('\n', code.Split('\n').Select(line => line.TrimStart().StartsWith("//") ? string.Empty : line));

	// Any method declaration beyond the expression-bodied constructor (which has no return type) is logic.
	[GeneratedRegex(@"\b(?:void|Task|bool|string|int|double|object)\s+\w+\s*\(")]
	private static partial Regex MethodDeclarationPattern();

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
