// Exercises the @WHISPER-84 native-theming requirement. Theming is pure WPF Presentation, so — like the
// packaging/guidance/accessibility drivers — this inspects repository artifacts directly rather than driving
// behavior through IMediator: it asserts the app opts into the built-in Fluent theme that follows the system
// Light/Dark + accent preference, and that the built-in-vs-library decision is recorded with its rationale.
// Honouring the live OS theme at runtime is verified by smoke; the prior M12 criteria still passing under the
// theme is the full non-@wip suite (the theme is app-level and does not touch the WPF-free logic).

using AwesomeAssertions;

namespace Dictation.Specs.Drivers;

public sealed class ThemingDriver
{
	private static readonly string RepositoryRoot = FindRepositoryRoot();

	public void AssertAppOptsIntoSystemFluentTheme()
	{
		string app = File.ReadAllText(Path.Combine(RepositoryRoot, "src", "Presentation", "App.xaml.cs"));
		app.Should().Contain("ThemeMode.System",
			"the app applies WPF's built-in Fluent theme following the OS Light/Dark + accent preference");
	}

	public void AssertThemingDecisionRecorded()
	{
		string path = Path.Combine(RepositoryRoot, "docs", "theming.md");
		File.Exists(path).Should().BeTrue("the theming decision must be recorded (WHISPER-84 AC2)");

		string doc = File.ReadAllText(path);
		doc.Should().Contain("ThemeMode", "the decision names the chosen built-in Fluent approach");
		doc.Should().Contain("Rationale", "the decision records its rationale");
		doc.Should().ContainAny("WPF-UI", "library", "iNKORE",
			"the decision weighs the built-in theme against a library alternative");
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
