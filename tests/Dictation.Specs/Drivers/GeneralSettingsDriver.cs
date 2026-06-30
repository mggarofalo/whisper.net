// Drives the start-at-login toggle through the REAL GeneralViewModel and its navigation activation
// (OnNavigatedTo -> first-activation load), over the same in-memory IStartupRegistration the
// run-on-login handlers use. Opening the section reads the registration through GetRunOnLoginQuery;
// flipping the toggle issues SetRunOnLoginCommand — so the view-model wiring and the Mediator
// round-trip both run for real, with only the OS registry seam faked. The Given/Then that seed and
// assert the registration are the shared run-on-login steps over that same scoped fake.

using AwesomeAssertions;
using Logic.AppManagement.Shell;

namespace Dictation.Specs.Drivers;

public sealed class GeneralSettingsDriver(GeneralViewModel viewModel)
{
	public void OpenSection() => viewModel.OnNavigatedTo();

	public void SetToggle(bool enabled) => viewModel.RunAtLogin = enabled;

	public void AssertToggle(bool expectedOn) =>
		viewModel.RunAtLogin.Should().Be(
			expectedOn, "the toggle should reflect the real startup registration when the section opens");
}
