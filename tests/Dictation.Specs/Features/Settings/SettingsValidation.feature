# Coverage map (acceptance criterion -> scenario / test):
#  AC1 invalid input renders a native adorner next to the offending control
#         -> WPF Validation.ErrorTemplate + AdornerDecorator in HotkeyView (Presentation glue, smoke).
#            The validation that drives the adorner (INotifyDataErrorInfo error surfaced for the field)
#            is proven WPF-free here: "An invalid hotkey surfaces a field error"
#  AC2 save/commit is blocked (no live-apply) while HasErrors is true
#         -> "An invalid hotkey blocks the save" + "A valid hotkey is persisted"
#  AC3 validation logic unit-tested in Logic.AppManagement independent of WPF
#         -> Logic.AppManagement.Tests/Shell/HotkeyViewModelValidationTests

@WHISPER-77
Feature: Settings input is validated natively before it can be saved
  As a user configuring dictation
  I want invalid settings to be flagged and refused
  So that a broken hotkey can never be persisted or applied

  Scenario: An invalid hotkey surfaces a field error
    Given the hotkey settings editor has loaded the current binding
    When the user enters the hotkey "Ctrl+Zorp"
    Then the hotkey field reports a validation error

  Scenario: An invalid hotkey blocks the save
    Given the hotkey settings editor has loaded the current binding
    When the user enters the hotkey "Ctrl+Zorp"
    And the user saves the hotkey
    Then no settings update is persisted

  Scenario: A valid hotkey is persisted
    Given the hotkey settings editor has loaded the current binding
    When the user enters the hotkey "Ctrl+Shift+J"
    And the user saves the hotkey
    Then the hotkey field reports no validation error
    And the binding "Ctrl+Shift+J" is persisted
