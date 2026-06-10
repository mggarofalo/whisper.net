# Coverage map (acceptance criterion -> scenario / test):
#  AC1 every interactive control has a meaningful UI Automation name; the hotkey control announces its binding
#         -> "Interactive settings controls have automation names" + "The hotkey control announces its binding"
#  AC2 the whole settings flow is operable by keyboard alone with a sensible tab order
#         -> "The settings views declare a logical tab order" (KeyboardNavigation.TabNavigation; the capture
#            control is a single, focusable tab stop)
#  AC3 a screen reader announces validation errors when they appear
#         -> MANUAL verification with Narrator / Accessibility Insights, tracked as a follow-up issue; the
#            error-surfacing path itself (INotifyDataErrorInfo + adorner) is built in WHISPER-77

@WHISPER-83
Feature: The settings UI is accessible
  As a user who relies on a keyboard and a screen reader
  I want the settings flow to be fully navigable and announced
  So that configuring dictation is not a sighted-mouse-only experience

  Scenario: Interactive settings controls have automation names
    Given the settings views
    Then the device picker controls have automation names
    And the model picker controls have automation names

  Scenario: The hotkey control announces its binding
    Given the settings views
    Then the hotkey controls have automation names

  Scenario: The settings views declare a logical tab order
    Given the settings views
    Then the settings views declare a keyboard tab order
