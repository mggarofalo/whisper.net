// Drives the @WHISPER-19 MVVM shell navigation scenarios. It owns HOW the shell is exercised so the
// steps stay one-liners: it resolves the REAL ShellViewModel (which composes the real NavigationService
// and feature view-models from the scenario's DI scope), navigates between sections, runs a feature
// view-model's Mediator-backed command over the faked model ports, and asserts at the view-model
// boundary. The thin WPF window that binds to ShellViewModel is Presentation glue verified by smoke.

using System.Reflection;
using Application.Ports;
using AwesomeAssertions;
using Domain.Models;
using Logic.AppManagement.Shell;
using NSubstitute;

namespace Dictation.Specs.Drivers;

public sealed class ShellNavigationDriver(ShellViewModel shell, IModelLifecycle lifecycle)
{
	// The active model id the faked lifecycle reports, so the Model section's Mediator round-trip (the
	// ListModelsQuery pipeline reading the lifecycle status) surfaces something the view-model could only
	// have learned by going through Mediator.
	private const string ActiveModel = "small.en";

	private object? _previous;
	private object? _remembered;

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

	// "Given a feature view model is active": set up the lifecycle the Model section's query reads, then
	// make the Model section the shell's active content.
	public void ActivateModelSection()
	{
		lifecycle.Status.Returns(new ModelStatus(ActiveModel, ModelState.Ready));
		shell.NavigateCommand.Execute("Model");
	}

	public async Task TriggerModelCommand() => await Active<ModelViewModel>().LoadCommand.ExecuteAsync(null);

	// Make the Model section active, load its list through the real Mediator pipeline (so ActiveModelId and
	// the Models collection are populated), then remember the instance so the round-trip can prove identity.
	public async Task LoadModelSectionAndRemember()
	{
		lifecycle.Status.Returns(new ModelStatus(ActiveModel, ModelState.Ready));
		shell.NavigateCommand.Execute("Model");
		await Active<ModelViewModel>().LoadCommand.ExecuteAsync(null);
		_remembered = shell.CurrentViewModel;
	}

	public void NavigateAwayAndBack(string away, string back)
	{
		shell.NavigateCommand.Execute(away);
		shell.NavigateCommand.Execute(back);
	}

	// The cached view-model is returned on the way back — not a freshly resolved one.
	public void AssertActiveIsRememberedInstance() =>
		shell.CurrentViewModel.Should().BeSameAs(_remembered, "navigating back must return the cached instance, not a new one");

	// The state the section held before navigating away is intact: the loaded list and the active selection.
	public void AssertModelSelectionSurvived()
	{
		ModelViewModel model = Active<ModelViewModel>();
		model.ActiveModelId.Should().Be(ActiveModel, "the active selection must survive a navigation round-trip");
		model.Models.Should().NotBeEmpty("the loaded model list must survive a navigation round-trip");
	}

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

	// The command learned the active model id only the real Mediator pipeline (the ListModels handler
	// reading the lifecycle status) could have produced — so the request went through Mediator, not a
	// direct port call.
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
