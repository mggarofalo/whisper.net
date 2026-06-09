# Coverage map (acceptance criterion -> scenario):
#  AC1 model step lists catalog models and downloads with live progress + terminal state
#         -> "Choosing a model downloads it with visible progress and activates it"
#  AC2 input-device step lists the actual capture devices to pick from
#         -> "Onboarding lists the available capture devices"
#  AC3 CanComplete gate (active model + chosen device) so Finish is disabled until usable
#         -> "Finish is blocked until a model and a device are chosen"
#  AC4 the onboarding window binds to the above -> WPF glue (smoke-only per the Presentation/specs split)

@WHISPER-74
Feature: Guided first-run onboarding
  As a new user on first run
  I want to pick from real devices and models with visible download progress
  So that setup is usable instead of a hand-typed form that downloads with no feedback

  Scenario: Onboarding lists the available capture devices
    Given the application has no completed setup
    When onboarding loads its choices
    Then the available capture devices are listed

  Scenario: Onboarding lists the catalog models
    Given the application has no completed setup
    When onboarding loads its choices
    Then the catalog models are listed

  Scenario: Choosing a model downloads it with visible progress and activates it
    Given the application has no completed setup
    And onboarding has loaded its choices
    When the user uses a model that is not yet downloaded
    Then the model download reports progress and the model becomes active

  Scenario: Finish is blocked until a model and a device are chosen
    Given the application has no completed setup
    When onboarding has loaded its choices
    Then onboarding cannot be completed yet
    And once a model and a device are chosen onboarding can be completed
