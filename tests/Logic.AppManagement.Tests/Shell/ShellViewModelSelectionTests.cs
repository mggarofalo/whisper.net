// Pins the data behind the nav region's selected-item visual: the shell tracks which
// section key is current so the nav button for the active section can be marked. The visual states
// (hover/pressed/focus/selected styling) are WPF and validated by smoke + the artifact spec; this pins
// that the selected key is correct on open and after navigation, since NavigateTo is only ever driven
// from here.

using AwesomeAssertions;
using Logic.AppManagement.Shell;
using Mediator;
using NSubstitute;
using Xunit;

namespace Logic.AppManagement.Tests.Shell;

public sealed class ShellViewModelSelectionTests
{
	private static INavigationService NavWith(params string[] sections)
	{
		INavigationService navigation = Substitute.For<INavigationService>();
		navigation.Sections.Returns(sections);
		return navigation;
	}

	// The shell loads the theme switcher on construction; a substitute mediator (no settings) is harmless
	// here — these tests only assert the navigation selected-key behavior.
	private static ShellViewModel NewShell(INavigationService navigation) =>
		new(navigation, new ThemeViewModel(Substitute.For<IMediator>()));

	[Fact]
	public void Opens_on_the_first_section_and_marks_it_selected()
	{
		INavigationService navigation = NavWith("Home", "Model", "History");

		ShellViewModel viewModel = NewShell(navigation);

		viewModel.CurrentSectionKey.Should().Be("Home");
		navigation.Received().NavigateTo("Home");
	}

	[Fact]
	public void Navigating_updates_the_selected_section_key()
	{
		INavigationService navigation = NavWith("Home", "Model", "History");
		ShellViewModel viewModel = NewShell(navigation);

		viewModel.NavigateCommand.Execute("History");

		viewModel.CurrentSectionKey.Should().Be("History");
		navigation.Received().NavigateTo("History");
	}

	[Fact]
	public void No_section_selected_when_none_are_registered()
	{
		ShellViewModel viewModel = NewShell(NavWith());

		viewModel.CurrentSectionKey.Should().BeNull();
	}
}
