# Coverage map (acceptance criterion -> scenario / test):
#  The sidebar follows the active light/dark theme (WHISPER-122) rather than a fixed dark rail
#  (WHISPER-103), and the selected tab uses the system accent. WCAG AA in both themes is the Fluent
#  theme's guarantee (default text on the themed surface; on-accent text on the accent) + a manual check.

@WHISPER-103
@WHISPER-122
Feature: The navigation sidebar follows the active theme
  As someone who switches Windows between light and dark
  I want the nav sidebar to follow the active theme and highlight the selected tab with my accent colour
  So that the navigation matches the rest of the window and never stays dark in light mode

  Scenario: The sidebar adapts to the theme rather than using a fixed dark palette
    Then the nav labels inherit the theme foreground rather than a fixed colour
    And the sidebar surface is a theme-neutral overlay, not a fixed dark panel

  Scenario: The selected tab uses the system accent colour
    Then the selected nav tab is painted with the system accent
    And the selected nav label uses the on-accent text colour

  Scenario: The nav button style is shared and defines every interaction state
    Then the shell window's sidebar uses shared brush resources with no hardcoded colour hex
    And the nav button style defines visible hover, pressed, focus, and selected states
