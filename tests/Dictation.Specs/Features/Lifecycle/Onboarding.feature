# Coverage map (acceptance criterion -> scenario / test):
#  AC1 first run with no completed setup shows onboarding        -> "First launch shows onboarding"
#  AC2 the flow guides model/audio/hotkey, dispatching commands  -> "Onboarding applies the chosen setup through the mediator"
#  AC3 any model download is explicitly user-initiated           -> "Model download is explicit"
#  AC4 completing marks setup complete (persisted), skips later  -> "Completing onboarding is remembered"
#  AC5 required permissions are checked and can be re-attempted  -> "Permissions can be re-attempted if denied"

@WHISPER-51
Feature: First-run onboarding
  As a new user
  I want a guided first-run setup
  So that the app is ready to dictate and never sets itself up behind my back

  Scenario: First launch shows onboarding
    Given the application has no completed setup
    When onboarding is evaluated at startup
    Then the onboarding flow is shown

  Scenario: Completing onboarding is remembered
    Given the user has completed the onboarding steps
    When onboarding is evaluated after a restart
    Then the onboarding flow is not shown again

  Scenario: Onboarding applies the chosen setup through the mediator
    Given the application has no completed setup
    When the user picks a model, an input device, and a hotkey
    Then the chosen setup is applied through the mediator

  Scenario: Model download is explicit
    Given onboarding offers to download a model
    When the user does not approve the download
    Then no model download occurs

  Scenario: Permissions can be re-attempted if denied
    Given the required permissions are denied at first
    When the user requests permissions and then re-attempts
    Then the permissions are reported as granted
