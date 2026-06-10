// Exercises the @WHISPER-93 declarative-event-wiring convention. Like the view-resolution driver, it
// inspects repository artifacts directly (the convention is pure WPF Presentation): no view outside
// the sanctioned input controls wires events in markup or code-behind, focus-on-activate is one
// reusable Behavior<T> applied via Interaction.Behaviors, the behaviors library is referenced, and the
// behavior-vs-command-vs-code-behind guideline (with the InvokeCommandAction CanExecute caveat) is
// committed to the architecture guide.

using System.Text.RegularExpressions;
using AwesomeAssertions;

namespace Dictation.Specs.Drivers;

public sealed partial class DeclarativeWiringDriver
{
	private static readonly string RepositoryRoot = FindRepositoryRoot();

	private static string PresentationRoot => Path.Combine(RepositoryRoot, "src", "Presentation");

	public void AssertNoEventWiringOutsideInputControls()
	{
		string controlsSegment = $"{Path.DirectorySeparatorChar}Controls{Path.DirectorySeparatorChar}";

		string[] markupOffenders = Directory
			.EnumerateFiles(PresentationRoot, "*.xaml", SearchOption.AllDirectories)
			.Where(file => !file.Contains(controlsSegment))
			.Where(file => EventHandlerAttributePattern().IsMatch(File.ReadAllText(file)))
			.ToArray();

		markupOffenders.Should().BeEmpty(
			"views express event reactions as commands or behaviors, not markup event handlers (WHISPER-93 AC1)");

		// App.xaml.cs is the composition root, not a view: its app-domain/dispatcher unhandled-exception
		// hooks are lifecycle wiring, not control-event wiring.
		string[] codeBehindOffenders = Directory
			.EnumerateFiles(PresentationRoot, "*.xaml.cs", SearchOption.AllDirectories)
			.Where(file => !file.Contains(controlsSegment) && Path.GetFileName(file) != "App.xaml.cs")
			.Where(file => File.ReadAllText(file).Contains("+="))
			.ToArray();

		codeBehindOffenders.Should().BeEmpty(
			"no view subscribes to control events in code-behind for VM behavior (WHISPER-93 AC1)");
	}

	public void AssertFocusBehaviorExists()
	{
		string path = Path.Combine(PresentationRoot, "Behaviors", "FocusOnActivateBehavior.cs");
		File.Exists(path).Should().BeTrue("focus-on-activate is a reusable attached behavior (WHISPER-93 AC2)");

		string code = File.ReadAllText(path);
		code.Should().Contain("Behavior<FrameworkElement>", "the behavior builds on the Xaml.Behaviors base type");
	}

	public void AssertFocusBehaviorAppliedDeclaratively()
	{
		string viewsRoot = Path.Combine(PresentationRoot, "Shell", "Views");
		bool applied = Directory
			.EnumerateFiles(viewsRoot, "*.xaml", SearchOption.AllDirectories)
			.Select(File.ReadAllText)
			.Any(xaml => xaml.Contains("Interaction.Behaviors") && xaml.Contains("FocusOnActivateBehavior"));

		applied.Should().BeTrue("at least one feature view applies the behavior through Interaction.Behaviors");
	}

	public void AssertNoPerViewLoadedHandler()
	{
		string[] offenders = Directory
			.EnumerateFiles(PresentationRoot, "*.xaml", SearchOption.AllDirectories)
			.Where(file => File.ReadAllText(file).Contains("Loaded=\""))
			.ToArray();

		offenders.Should().BeEmpty("focus-on-activate replaced the per-view Loaded-handler pattern (WHISPER-93 AC2)");
	}

	public void AssertBehaviorsLibraryReferenced()
	{
		string csproj = File.ReadAllText(Path.Combine(PresentationRoot, "Presentation.csproj"));
		csproj.Should().Contain("Microsoft.Xaml.Behaviors.Wpf", "the behaviors library is referenced (WHISPER-93 AC3)");

		string packages = File.ReadAllText(Path.Combine(RepositoryRoot, "Directory.Packages.props"));
		packages.Should().Contain("Microsoft.Xaml.Behaviors.Wpf", "the central package table pins its version");
	}

	public void AssertGuidelineDocumented()
	{
		string doc = File.ReadAllText(Path.Combine(RepositoryRoot, "docs", "architecture.md"));

		doc.Should().Contain("behavior vs command vs legitimate code-behind",
			"the wiring guideline has its own documented section (WHISPER-93 AC3)");
		doc.Should().Contain("InvokeCommandAction", "the guideline names the trigger action");
		doc.Should().Contain("CanExecute", "the guideline records that InvokeCommandAction does not honor enablement");
		doc.Should().Contain("FocusOnActivateBehavior", "the guideline points at the reusable behavior");
	}

	// Markup event-handler wiring: an attribute whose value names a handler method (Foo="OnBar"). Command,
	// binding, and property attributes never reference On-handlers, so this pins exactly the anti-pattern.
	[GeneratedRegex(@"\s\w+=""On\w+""")]
	private static partial Regex EventHandlerAttributePattern();

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
