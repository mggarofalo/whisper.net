// Exercises the @WHISPER-103 sidebar-contrast requirement. The sidebar restyle is pure WPF Presentation,
// so — like the theming/accessibility drivers — this inspects repository artifacts directly rather than
// driving behavior through IMediator. It proves the two checkable facts behind the issue: every nav
// foreground/background pair clears WCAG 2.1 AA contrast (computed from the ACTUAL brush colours shipped
// in ShellResources.xaml, so the numbers can't drift from what renders), and the sidebar takes its colours
// from shared brush resources with no view-local hex (AC3), with the nav button style declaring every
// required state (AC2). The live rendered states are verified by smoke + a manual check.

using System.Globalization;
using System.Text.RegularExpressions;
using AwesomeAssertions;

namespace Dictation.Specs.Drivers;

public sealed class SidebarThemeDriver
{
	private static readonly string RepositoryRoot = FindRepositoryRoot();

	private readonly string _resources = File.ReadAllText(Path.Combine(RepositoryRoot, "src", "Presentation", "Shell", "ShellResources.xaml"));
	private readonly string _window = File.ReadAllText(Path.Combine(RepositoryRoot, "src", "Presentation", "Shell", "ShellWindow.xaml"));

	public void AssertLabelContrastMeetsAa()
	{
		string rail = Hex("NavSidebarBackgroundBrush");
		string label = Hex("NavItemForegroundBrush");
		string hover = Hex("NavItemHoverBackgroundBrush");
		string pressed = Hex("NavItemPressedBackgroundBrush");
		string selectedBackground = Hex("NavItemSelectedBackgroundBrush");
		string selectedForeground = Hex("NavItemSelectedForegroundBrush");

		Contrast(label, rail).Should().BeGreaterThanOrEqualTo(4.5, "default nav labels must meet WCAG AA against the rail");
		Contrast(label, hover).Should().BeGreaterThanOrEqualTo(4.5, "hovered nav labels must meet WCAG AA");
		Contrast(label, pressed).Should().BeGreaterThanOrEqualTo(4.5, "pressed nav labels must meet WCAG AA");
		Contrast(selectedForeground, selectedBackground).Should().BeGreaterThanOrEqualTo(4.5, "the selected nav label must meet WCAG AA");
	}

	public void AssertSelectedAccentIsDistinct() =>
		Contrast(Hex("NavItemAccentBrush"), Hex("NavSidebarBackgroundBrush"))
			.Should().BeGreaterThanOrEqualTo(3.0, "the selected-item accent must be a visible non-text indicator (WCAG 1.4.11)");

	public void AssertSidebarUsesSharedBrushesNoHex()
	{
		_window.Should().Contain("Background=\"{StaticResource NavSidebarBackgroundBrush}\"",
			"the sidebar background is a shared brush, not a hex literal");
		_window.Should().Contain("Style=\"{StaticResource NavButtonStyle}\"",
			"the nav buttons use the shared templated style");
		Regex.IsMatch(_window, "\"#[0-9A-Fa-f]{6,8}\"").Should().BeFalse(
			"no view-local colour hex may remain in the shell window (WHISPER-103 AC3)");
	}

	public void AssertNavStyleDefinesAllStates()
	{
		_resources.Should().Contain("x:Key=\"NavButtonStyle\"");
		_resources.Should().Contain("IsMouseOver", "the nav style defines a visible hover state");
		_resources.Should().Contain("IsPressed", "the nav style defines a visible pressed state");
		_resources.Should().Contain("IsKeyboardFocused", "the nav style defines a visible keyboard-focus state");
		_resources.Should().Contain("CurrentSectionKey", "the selected state is driven by the active section key");
	}

	private string Hex(string brushKey)
	{
		Match match = Regex.Match(_resources, $"x:Key=\"{Regex.Escape(brushKey)}\"\\s+Color=\"(#[0-9A-Fa-f]{{6}})\"");
		match.Success.Should().BeTrue($"the '{brushKey}' brush must be defined in ShellResources.xaml with an explicit colour");
		return match.Groups[1].Value;
	}

	// --- WCAG 2.1 relative-luminance contrast ---

	private static double Contrast(string hexA, string hexB)
	{
		double a = Luminance(hexA);
		double b = Luminance(hexB);
		double lighter = Math.Max(a, b);
		double darker = Math.Min(a, b);
		return (lighter + 0.05) / (darker + 0.05);
	}

	private static double Luminance(string hex)
	{
		int r = int.Parse(hex.Substring(1, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
		int g = int.Parse(hex.Substring(3, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
		int b = int.Parse(hex.Substring(5, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
		return (0.2126 * Channel(r)) + (0.7152 * Channel(g)) + (0.0722 * Channel(b));
	}

	private static double Channel(int component)
	{
		double s = component / 255.0;
		return s <= 0.03928 ? s / 12.92 : Math.Pow((s + 0.055) / 1.055, 2.4);
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
