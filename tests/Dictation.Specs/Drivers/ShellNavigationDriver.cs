// Drives the @WHISPER-19 MVVM shell navigation scenarios. It owns HOW the shell is exercised so the
// steps stay one-liners: it resolves the REAL ShellViewModel (which composes the real NavigationService
// and feature view-models from the scenario's DI scope), navigates between sections, runs a feature
// view-model's Mediator-backed command over the faked ISettingsStore, and asserts at the view-model
// boundary. The thin WPF window that binds to ShellViewModel is Presentation glue verified by smoke.

using System.Reflection;
using Application.Ports;
using AwesomeAssertions;
using Domain.Settings;
using Logic.AppManagement.Shell;
using NSubstitute;

namespace Dictation.Specs.Drivers;

public sealed class ShellNavigationDriver(ShellViewModel shell, ISettingsStore store)
{
	// The active model id the faked store reports, so the Model section's Mediator round-trip returns
	// something only the real GetSettingsQuery pipeline could have supplied.
	private const string ActiveModel = "small.en";

	private object? _previous;

	// The shell view-model navigates to its first section (Home) on construction, so resolving it has
	// already "opened" the shell; this just makes the Given explicit.
	public void OpenShell() => _ = shell;

	public void Navigate(string section) => shell.NavigateCommand.Execute(section);

	// Capture the outgoing view-model before navigating, so the deactivation assertion can inspect it.
	public void NavigateCapturingPrevious(string section)
	{
		_previous = shell.CurrentViewModel;
		shell.NavigateCommand.Execute(section);
	}

	// "Given a feature view model is active": set up the store the Model section's query reads, then make
	// the Model section the shell's active content.
	public void ActivateModelSection()
	{
		store.LoadAsync(Arg.Any<CancellationToken>())
			.Returns(new AppSettings(ActiveModel, HotkeyBinding.Parse("Ctrl+Shift+D"), silenceThresholdMs: 700, fillerWordRemovalEnabled: false));
		shell.NavigateCommand.Execute("Model");
	}

	public async Task TriggerModelCommand() => await Active<ModelViewModel>().LoadCommand.ExecuteAsync(null);

	// --- assertions ---

	public void AssertActiveIsModelView() => shell.CurrentViewModel.Should().BeOfType<ModelViewModel>();

	public void AssertActiveIsHomeView() => shell.CurrentViewModel.Should().BeOfType<HomeViewModel>();

	// A container-resolved view-model has its IMediator dependency injected; a hand-constructed one would
	// not. Proving the private field is non-null shows the view-model came from the DI container.
	public void AssertActiveResolvedFromContainer()
	{
		ModelViewModel model = Active<ModelViewModel>();
		object? mediator = typeof(ModelViewModel)
			.GetField("_mediator", BindingFlags.Instance | BindingFlags.NonPublic)!
			.GetValue(model);
		mediator.Should().NotBeNull("a container-resolved view-model has its IMediator dependency injected");
	}

	// The command learned the active model id only the real Mediator pipeline (handler + mapper over the
	// store) could have produced — so the request went through Mediator, not a direct port call.
	public void AssertRequestWentThroughMediator() =>
		Active<ModelViewModel>().ActiveModelId.Should().Be(ActiveModel);

	// The feature view-model depends only on IMediator: none of its fields come from the Infrastructure
	// assembly/namespace.
	public void AssertModelViewModelHoldsNoInfrastructureReference()
	{
		IEnumerable<Type> fieldTypes = typeof(ModelViewModel)
			.GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
			.Select(field => field.FieldType);

		fieldTypes.Should().NotContain(
			type => (type.Namespace ?? string.Empty).StartsWith("Infrastructure", StringComparison.Ordinal),
			"a feature view-model must depend only on IMediator, never Infrastructure");
	}

	public void AssertPreviousModelDeactivated() =>
		(_previous as ModelViewModel)!.IsActive.Should().BeFalse("navigating away should deactivate the previous view-model");

	private T Active<T>() where T : class =>
		shell.CurrentViewModel.Should().BeOfType<T>().Subject;
}
