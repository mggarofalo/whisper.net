// Inner TDD loop for the theme switcher's view-model (WHISPER-121), WPF-free. Pins that it loads the
// persisted preference for display, that a genuine user pick persists the new theme through
// UpdateSettings (carrying the whole settings DTO with the theme swapped), and that the programmatic
// selection a load performs does NOT commit (the same suppress-on-load discipline as the audio picker).

using Application.Settings;
using AwesomeAssertions;
using Domain.Settings;
using Logic.AppManagement.Shell;
using Mediator;
using NSubstitute;
using Xunit;

namespace Logic.AppManagement.Tests.Shell;

public sealed class ThemeViewModelTests
{
	private static AppSettingsDto DtoWith(ThemePreference preference) =>
		new("base.en", "Ctrl+Win", 500, FillerWordRemovalEnabled: true, ThemePreference: preference);

	private static IMediator MediatorReturning(ThemePreference persisted)
	{
		IMediator mediator = Substitute.For<IMediator>();
		mediator.Send(Arg.Any<GetSettingsQuery>(), Arg.Any<CancellationToken>())
			.Returns(new ValueTask<AppSettingsDto>(DtoWith(persisted)));
		mediator.Send(Arg.Any<UpdateSettingsCommand>(), Arg.Any<CancellationToken>())
			.Returns(new ValueTask<Unit>(Unit.Value));
		return mediator;
	}

	[Fact]
	public void Loads_the_persisted_preference_for_display()
	{
		IMediator mediator = MediatorReturning(ThemePreference.Dark);
		ThemeViewModel viewModel = new(mediator);

		viewModel.LoadCommand.Execute(null);

		viewModel.SelectedTheme.Should().Be(ThemePreference.Dark);
	}

	[Fact]
	public void Loading_the_preference_does_not_persist_it()
	{
		IMediator mediator = MediatorReturning(ThemePreference.Dark);
		ThemeViewModel viewModel = new(mediator);

		viewModel.LoadCommand.Execute(null);

		mediator.DidNotReceive().Send(Arg.Any<UpdateSettingsCommand>(), Arg.Any<CancellationToken>());
	}

	[Fact]
	public void A_user_pick_persists_the_new_theme_keeping_the_rest_of_settings()
	{
		IMediator mediator = MediatorReturning(ThemePreference.System);
		ThemeViewModel viewModel = new(mediator);
		viewModel.LoadCommand.Execute(null);

		viewModel.SelectedTheme = ThemePreference.Dark;

		mediator.Received(1).Send(
			Arg.Is<UpdateSettingsCommand>(command =>
				command.Settings.ThemePreference == ThemePreference.Dark && command.Settings.ModelId == "base.en"),
			Arg.Any<CancellationToken>());
	}
}
