# Coverage map (acceptance criterion -> scenario / test):
#  The user can choose System / Light / Dark; the choice is persisted and applied app-wide.
#         -> "Choosing a theme persists it and survives a reload" (the switcher VM over the real Mediator
#            + round-tripping store). Applying the choice to WPF's ThemeMode (and the System default
#            following the OS) is the App's job and a manual remainder.

@WHISPER-121
Feature: A theme switcher lets the user choose Light, Dark, or System
  As someone who prefers a specific theme
  I want to pick Light, Dark, or System and have it remembered
  So that the app is not stuck on one theme and follows my choice across launches

  Scenario: Choosing a theme persists it and survives a reload
    Given the theme switcher is loaded showing the system theme
    When the user selects the "Dark" theme
    Then the dark theme is persisted
    And reopening the switcher shows the dark theme
