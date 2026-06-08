# Coverage map (acceptance criterion -> scenario / test):
#  AC1 shell hosts a nav region + resolves views/VMs via the DI container -> "Navigating selects the requested view"
#  AC2 VMs use CommunityToolkit.Mvvm + depend only on IMediator (no ports)  -> "View model actions go through the mediator" (no-infra step) + ModelViewModel/ShellViewModel structure
#  AC3 navigating activates the correct VM and deactivates the previous one -> "Navigating away deactivates the previous view"
#  AC4 a representative VM action dispatches a Mediator command/query        -> "View model actions go through the mediator"

@WHISPER-19
Feature: MVVM shell navigation
  As the dashboard
  I want a navigable shell whose sections are resolved from the DI container and talk through Mediator
  So that feature views plug in cleanly without holding their own infrastructure

  Scenario: Navigating selects the requested view
    Given the dashboard shell is open
    When the user navigates to the "Model" section
    Then the model view becomes the active content
    And its view model is resolved from the container

  Scenario: View model actions go through the mediator
    Given a feature view model is active
    When the user triggers a command on that view model
    Then the request is sent via the mediator
    And the view model holds no direct reference to infrastructure

  Scenario: Navigating away deactivates the previous view
    Given the dashboard shell has navigated to the "Model" section
    When the user navigates to the "Home" section
    Then the model view model is deactivated
    And the home view becomes the active content
