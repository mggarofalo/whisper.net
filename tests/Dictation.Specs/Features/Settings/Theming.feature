# Coverage map (acceptance criterion -> scenario / test):
#  AC1 app honors system Light/Dark + accent; settings window uses the chosen theme, no functional regressions
#         -> "The app opts into the system Fluent theme" (the ThemeMode.System opt-in); the themed window and
#            live-theme switching are verified by smoke, and "no functional regressions" is the full suite (AC3)
#  AC2 decision recorded: built-in Fluent vs library, with rationale
#         -> "The theming decision is recorded"
#  AC3 all prior M12 criteria still pass under the theme
#         -> the full non-@wip suite is green with the theme applied (the theme is app-level; the WPF-free
#            validation / live-apply / picker / accessibility logic is unaffected)

@WHISPER-84
Feature: The settings UI uses a native Fluent theme
  As a Windows user
  I want the app to match my system light/dark and accent
  So that the settings window looks native, without adding a UI dependency

  Scenario: The app opts into the system Fluent theme
    Given the presentation layer
    Then the app applies the built-in Fluent theme following the system preference

  Scenario: The theming decision is recorded
    Given the presentation layer
    Then the built-in-versus-library theming decision is recorded with rationale
