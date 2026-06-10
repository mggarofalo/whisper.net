# Coverage map (acceptance criterion -> scenario / test):
#  AC1 the standalone OnboardingWindow + OnboardingViewModel are deleted; first-run reuses the settings VMs
#         -> the deletion of those files + this feature driving the real settings pipeline; the pickers are
#            the @WHISPER-79/-80/-81 settings views/VMs
#  AC2 on launch the settings window opens when there is no active model or setup is incomplete; else tray
#         -> "A fresh install is not configured", "A completed setup with a cached model is configured",
#            "A completed setup whose model is gone is not configured" +
#            Application.Tests/Settings/GetSetupStatusHandlerTests
#  AC3 completing setup (a model active) marks it done and does not re-prompt; no field in two places
#         -> "Activating a model marks setup complete" (SwitchActiveModel marks setup done); the single
#            settings VMs are the only place each field lives now that OnboardingViewModel is gone

@WHISPER-82
Feature: First-run setup happens in the settings window, not a separate onboarding flow
  As a new user
  I want the app to open settings on first launch and then stay out of my way
  So that setup and settings are the same place and never drift

  Scenario: A fresh install is not configured
    When the launch setup check runs
    Then the app is not configured

  Scenario: A completed setup with a cached model is configured
    Given setup was completed
    And the model "base.en" is downloaded
    When the launch setup check runs
    Then the app is configured

  Scenario: A completed setup whose model is gone is not configured
    Given setup was completed
    And the model "base.en" is not downloaded
    When the launch setup check runs
    Then the app is not configured

  Scenario: Activating a model marks setup complete
    Given the model "small.en" is downloaded
    When the user activates the model "small.en"
    And the launch setup check runs
    Then the app is configured
