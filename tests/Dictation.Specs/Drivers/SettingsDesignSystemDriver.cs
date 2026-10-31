// Exercises the settings-shell design-system requirement (WHISPER-142). The restyle is pure WPF
// Presentation, so, like the sidebar-theme and accessibility drivers, this inspects the repository's XAML
// directly rather than driving behavior through IMediator. It proves the checkable facts: the feature views
// carry no colour literals and no Opacity-dimmed text, status colours are Fluent semantic brushes bound by
// DynamicResource in the one shared dictionary, every view uses the shared type scale (no ad-hoc font
// sizes), each page title is its nav label, the scrolling pages reserve the scrollbar gutter, the window has
// a minimum size, and list rows expose meaningful automation names. The rendered result in both themes is
// verified by screenshots in the PR (a manual remainder), not here.

using System.Text.RegularExpressions;
using AwesomeAssertions;
using Logic.AppManagement.Shell;

namespace Dictation.Specs.Drivers;

public sealed partial class SettingsDesignSystemDriver(IEnumerable<NavigationSection> sections)
{
	private static readonly string ShellDirectory = Path.Combine(FindRepositoryRoot(), "src", "Presentation", "Shell");

	private static readonly string Resources = File.ReadAllText(Path.Combine(ShellDirectory, "ShellResources.xaml"));
	private static readonly string Window = File.ReadAllText(Path.Combine(ShellDirectory, "ShellWindow.xaml"));

	private static readonly IReadOnlyDictionary<string, string> Views = Directory
		.GetFiles(Path.Combine(ShellDirectory, "Views"), "*.xaml")
		.ToDictionary(path => Path.GetFileNameWithoutExtension(path)!, File.ReadAllText);

	public void AssertViewsHoldNoColourLiterals()
	{
		foreach ((string view, string xaml) in Views)
		{
			ColourLiteral().IsMatch(xaml).Should().BeFalse($"{view} must take its colours from the shared dictionary");
		}
	}

	public void AssertStatusColoursAreSemanticBrushes()
	{
		string[] semantic =
		[
			"SystemFillColorCriticalBrush", "SystemFillColorCautionBrush", "DividerStrokeColorDefaultBrush",
			"AccentFillColorDefaultBrush", "TextOnAccentFillColorPrimaryBrush", "TextFillColorSecondaryBrush",
		];
		foreach (string brush in semantic)
		{
			Resources.Should().Contain($"{{DynamicResource {brush}}}", $"{brush} is bound live so it tracks the theme");
		}

		// The light-theme-only literals that failed in dark mode are gone everywhere in the shell.
		foreach (string literal in new[] { "#E0E0E0", "#B06000", "#C03030", "#308AE0", "#FB8C00" })
		{
			Resources.Should().NotContainEquivalentOf(literal);
			Views.Values.Should().AllSatisfy(xaml => xaml.Should().NotContainEquivalentOf(literal));
		}
	}

	public void AssertSecondaryTextUsesTheSecondaryBrush()
	{
		foreach ((string view, string xaml) in Views)
		{
			xaml.Should().NotContain("Opacity=", $"{view} dims secondary text with the secondary brush, not opacity");
		}

		Window.Should().NotContain("Opacity=", "the shell window's captions use the secondary brush too");
	}

	public void AssertViewsUseTheSharedTypeScale()
	{
		foreach (string style in new[] { "PageTitle", "SectionHeader", "BodyText", "CaptionText" })
		{
			Resources.Should().Contain($"x:Key=\"{style}\"", $"the shared type scale defines {style}");
		}

		foreach (string spacing in new[] { "PageMargin", "GapTight", "GapGroup", "GapSection" })
		{
			Resources.Should().Contain($"x:Key=\"{spacing}\"", $"the shared spacing tokens define {spacing}");
		}

		foreach ((string view, string xaml) in Views)
		{
			xaml.Should().NotContain("FontSize=", $"{view} takes its type sizes from the shared scale");
			xaml.Should().Contain("{DynamicResource PageMargin}", $"{view} uses the shared page margin");
		}
	}

	public void AssertPageTitlesMatchNavLabels()
	{
		foreach (NavigationSection section in sections)
		{
			string view = section.ViewModelType.Name.Replace("ViewModel", "View", StringComparison.Ordinal);
			Views.Should().ContainKey(view);

			Match title = PageTitle().Match(Views[view]);
			title.Success.Should().BeTrue($"{view} has a PageTitle");
			title.Groups["text"].Value.Should().Be(section.Key, $"the {view} page title is its nav label");
		}
	}

	public void AssertPageCommandsLeaveTheTitleRow()
	{
		// History's Load more is at the END of the list (after the ItemsControl), not in the header.
		string history = Views["HistoryView"];
		history.IndexOf("Load more", StringComparison.Ordinal)
			.Should().BeGreaterThan(history.IndexOf("</ItemsControl>", StringComparison.Ordinal),
				"Load more sits below the list, where the reader is");

		// No page header is a horizontal StackPanel mixing the title with buttons.
		foreach ((string view, string xaml) in Views)
		{
			TitleInHorizontalRow().IsMatch(xaml).Should().BeFalse($"{view}'s title stands on its own, not in a toolbar row");
		}
	}

	public void AssertScrollingPagesReserveTheScrollbarGutter()
	{
		Resources.Should().MatchRegex(
			"x:Key=\"ContentScrollViewer\"[\\s\\S]*?Property=\"Padding\"\\s+Value=\"0,0,16,0\"",
			"the shared scroll style reserves a right gutter for the overlay scrollbar");

		foreach (string view in new[] { "HomeView", "HistoryView", "ModelView" })
		{
			Views[view].Should().Contain("Style=\"{DynamicResource ContentScrollViewer}\"",
				$"{view}'s scrollbar must not cover its row actions");
		}
	}

	public void AssertWindowHasAMinimumSize()
	{
		Window.Should().MatchRegex("MinWidth=\"\\d+\"", "the shell window cannot shrink below a usable width");
		Window.Should().MatchRegex("MinHeight=\"\\d+\"", "the shell window cannot shrink below a usable height");
	}

	public void AssertListRowsHaveMeaningfulAutomationNames()
	{
		foreach ((string view, string bound) in new[] { ("HistoryView", "Text"), ("HomeView", "Text"), ("ModelView", "DisplayName") })
		{
			Views[view].Should().MatchRegex(
				$"<ItemsControl\\.ItemContainerStyle>[\\s\\S]*?Property=\"AutomationProperties\\.Name\"\\s+Value=\"\\{{Binding {bound}\\}}\"",
				$"{view}'s rows are announced by their {bound}, not the item's ToString");
		}
	}

	[GeneratedRegex("\"#[0-9A-Fa-f]{3,8}\"")]
	private static partial Regex ColourLiteral();

	[GeneratedRegex("<TextBlock\\s+Text=\"(?<text>[^\"]+)\"\\s+Style=\"\\{DynamicResource PageTitle\\}\"")]
	private static partial Regex PageTitle();

	[GeneratedRegex("Orientation=\"Horizontal\"[^>]*>\\s*<TextBlock[^>]*(FontSize=\"18\"|PageTitle)")]
	private static partial Regex TitleInHorizontalRow();

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
