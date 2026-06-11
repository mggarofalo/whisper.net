// Drives the @WHISPER-121 theme-switcher scenarios over the REAL ThemeViewModel and Mediator pipeline
// (GetSettings to load, UpdateSettings to persist + broadcast), with a round-tripping settings store: a
// SaveAsync is reflected in the next LoadAsync, so choosing a theme is observed as the persisted
// ThemePreference and survives a reload. Applying the choice to WPF's ThemeMode is the App's job and a
// manual remainder.

using Application.Ports;
using Application.Settings;
using AwesomeAssertions;
using Domain.Settings;
using Logic.AppManagement.Shell;
using Mediator;
using NSubstitute;

namespace Dictation.Specs.Drivers;

public sealed class ThemeSwitcherDriver
{
	private readonly IMediator _mediator;
	private readonly ThemeViewModel _viewModel;

	private AppSettings _persisted = AppSettings.Default;

	public ThemeSwitcherDriver(IMediator mediator, ISettingsStore store)
	{
		_mediator = mediator;

		// The settings store round-trips a save into the next load, so a theme change can be observed as the
		// persisted ThemePreference and survives a reload.
		store.LoadAsync(Arg.Any<CancellationToken>()).Returns(_ => _persisted);
		store.When(s => s.SaveAsync(Arg.Any<AppSettings>(), Arg.Any<CancellationToken>()))
			.Do(call => _persisted = call.Arg<AppSettings>());

		_viewModel = new ThemeViewModel(mediator);
	}

	public Task LoadSwitcher() => _viewModel.LoadCommand.ExecuteAsync(null);

	public void SelectTheme(string theme) => _viewModel.SelectedTheme = Parse(theme);

	public void AssertSwitcherShows(string theme) => _viewModel.SelectedTheme.Should().Be(Parse(theme));

	public void AssertPersisted(string theme) =>
		_persisted.ThemePreference.Should().Be(Parse(theme), "the chosen theme is saved");

	public async Task AssertReopeningShows(string theme)
	{
		ThemeViewModel reopened = new(_mediator);
		await reopened.LoadCommand.ExecuteAsync(null);
		reopened.SelectedTheme.Should().Be(Parse(theme), "the persisted theme survives a reload");
	}

	private static ThemePreference Parse(string theme) => Enum.Parse<ThemePreference>(theme, ignoreCase: true);
}
