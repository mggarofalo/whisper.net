// Exercises the navigation-sidebar theming requirement (WHISPER-103, made theme-adaptive in WHISPER-122).
// The restyle is pure WPF Presentation, so — like the theming/accessibility drivers — this inspects
// repository artifacts directly rather than driving behavior through IMediator. It proves the checkable
// facts: the sidebar follows the active theme (labels inherit the theme foreground; the rail is a
// theme-neutral overlay rather than a fixed dark panel), the selected tab uses the system accent colour
// (the Fluent AccentFillColorDefault brush + on-accent text), the sidebar carries no view-local colour
// hex, and the nav button style declares every interaction state. WCAG AA in both light and dark is the
// Fluent theme's guarantee (default text on the themed surface; on-accent text on the accent) plus a
// manual visual check, not a fixed computed value.

using System.Text.RegularExpressions;
using AwesomeAssertions;

namespace Dictation.Specs.Drivers;

public sealed class SidebarThemeDriver
{
	private static readonly string RepositoryRoot = FindRepositoryRoot();

	private readonly string _resources = File.ReadAllText(Path.Combine(RepositoryRoot, "src", "Presentation", "Shell", "ShellResources.xaml"));
	private readonly string _window = File.ReadAllText(Path.Combine(RepositoryRoot, "src", "Presentation", "Shell", "ShellWindow.xaml"));

	public void AssertLabelsInheritTheTheme()
	{
		// No pinned label brush — the nav label inherits the theme's foreground, so it adapts to light/dark.
		_resources.Should().NotContain("NavItemForegroundBrush",
			"the nav label must inherit the theme foreground, not a fixed colour (WHISPER-122)");
		// The fixed dark palette from the original rail is gone.
		_resources.Should().NotContain("#1E1E1E", "the rail is no longer a fixed dark panel");
		_resources.Should().NotContain("#0E639C", "the selected tab is no longer a fixed blue");
	}

	public void AssertSidebarSurfaceIsThemeNeutral() =>
		Regex.IsMatch(_resources, "x:Key=\"NavSidebarBackgroundBrush\"\\s+Color=\"#[0-3][0-9A-Fa-f]808080\"")
			.Should().BeTrue("the rail surface is a low-alpha neutral gray that reads on both light and dark themes");

	public void AssertSelectedTabUsesTheSystemAccent() =>
		_resources.Should().Contain("AccentFillColorDefaultBrush",
			"the selected tab is painted with the system accent (Fluent AccentFillColorDefault)");

	public void AssertSelectedLabelUsesOnAccentText() =>
		_resources.Should().Contain("TextOnAccentFillColorPrimaryBrush",
			"the selected tab's label uses the theme's on-accent text colour");

	public void AssertSidebarUsesSharedBrushesNoHex()
	{
		_window.Should().Contain("Background=\"{StaticResource NavSidebarBackgroundBrush}\"",
			"the sidebar background is a shared brush, not a hex literal");
		_window.Should().Contain("Style=\"{StaticResource NavButtonStyle}\"",
			"the nav buttons use the shared templated style");
		Regex.IsMatch(_window, "\"#[0-9A-Fa-f]{6,8}\"").Should().BeFalse(
			"no view-local colour hex may remain in the shell window");
	}

	public void AssertNavStyleDefinesAllStates()
	{
		_resources.Should().Contain("x:Key=\"NavButtonStyle\"");
		_resources.Should().Contain("IsMouseOver", "the nav style defines a visible hover state");
		_resources.Should().Contain("IsPressed", "the nav style defines a visible pressed state");
		_resources.Should().Contain("IsKeyboardFocused", "the nav style defines a visible keyboard-focus state");
		_resources.Should().Contain("CurrentSectionKey", "the selected state is driven by the active section key");
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
