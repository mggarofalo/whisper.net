// Thin step definitions for the MVVM shell navigation feature. Each step delegates to the
// ShellNavigationDriver (injected by the Reqnroll DI plugin); no logic lives here.

using Dictation.Specs.Drivers;
using Reqnroll;

namespace Dictation.Specs.StepDefinitions;

[Binding]
public sealed class ShellNavigationSteps(ShellNavigationDriver driver)
{
	[Given(@"the dashboard shell is open")]
	public void GivenTheDashboardShellIsOpen() => driver.OpenShell();

	[Given(@"a feature view model is active")]
	public void GivenAFeatureViewModelIsActive() => driver.ActivateModelSection();

	[Given(@"the dashboard shell has navigated to the ""(.*)"" section")]
	public void GivenTheShellHasNavigatedTo(string section) => driver.Navigate(section);

	[When(@"the user navigates to the ""Model"" section")]
	public void WhenTheUserNavigatesToTheModelSection() => driver.Navigate("Model");

	[When(@"the user navigates to the ""Home"" section")]
	public void WhenTheUserNavigatesToTheHomeSection() => driver.NavigateCapturingPrevious("Home");

	[When(@"the user triggers a command on that view model")]
	public async Task WhenTheUserTriggersACommand() => await driver.TriggerModelCommand();

	[Then(@"the model view becomes the active content")]
	public void ThenTheModelViewBecomesActive() => driver.AssertActiveIsModelView();

	[Then(@"its view model is resolved from the container")]
	public void ThenItsViewModelIsResolvedFromTheContainer() => driver.AssertActiveResolvedFromContainer();

	[Then(@"the request is sent via the mediator")]
	public void ThenTheRequestIsSentViaTheMediator() => driver.AssertRequestWentThroughMediator();

	[Then(@"the view model holds no direct reference to infrastructure")]
	public void ThenTheViewModelHoldsNoInfrastructureReference() => driver.AssertModelViewModelHoldsNoInfrastructureReference();

	[Then(@"the model view model is deactivated")]
	public void ThenTheModelViewModelIsDeactivated() => driver.AssertPreviousModelDeactivated();

	[Then(@"the home view becomes the active content")]
	public void ThenTheHomeViewBecomesActive() => driver.AssertActiveIsHomeView();
}
