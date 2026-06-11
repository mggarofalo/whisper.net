# Coverage map (acceptance criterion -> scenario):
#  AC1 all nav labels meet WCAG AA contrast
#         -> "Every nav label/background pair meets WCAG AA contrast" (contrast computed from the actual
#            shipped brush colours)
#  AC2 hover, selected, and focus states are visible
#         -> "The nav button style defines visible hover, pressed, focus, and selected states" +
#            Logic.AppManagement.Tests/Shell/ShellViewModelSelectionTests (the selected-key data) +
#            the WHISPER-96 smoke harness (the styled view parses and binds)
#  AC3 colours come from shared theme resources, not view-local hex
#         -> "The sidebar takes its colours from shared theme resources, not view-local hex"

@WHISPER-103
Feature: The navigation sidebar meets dark-theme contrast
  As someone using the settings window
  I want the nav sidebar styled for the dark theme with legible labels and clear states
  So that the navigation is readable and accessible, not a near-white panel with faint text

  Scenario: Every nav label/background pair meets WCAG AA contrast
    Then each nav label colour has at least 4.5 to 1 contrast against its background
    And the selected-item accent has at least 3 to 1 contrast against the sidebar

  Scenario: The sidebar takes its colours from shared theme resources, not view-local hex
    Then the shell window's sidebar uses shared brush resources with no hardcoded colour hex
    And the nav button style defines visible hover, pressed, focus, and selected states
