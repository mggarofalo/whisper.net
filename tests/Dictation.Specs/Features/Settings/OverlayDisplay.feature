@WHISPER-139
Feature: Overlay display setting
  The General settings section lets the user choose which display the recording overlay appears on,
  defaulting to the primary display. The choice persists and survives a reload; a display that is no
  longer attached falls back to the primary, so the overlay is never stranded off-screen.

  Scenario: The picker defaults to the primary display
    When the user opens the overlay display settings
    Then the primary display is offered as the default
    And the overlay display selection follows the primary by default

  Scenario: Choosing another display persists it and survives a reload
    Given a second display is attached
    And the user opens the overlay display settings
    Then the second display is listed as a choice
    When the user selects the second display
    Then the overlay display is persisted as the second display
    And reopening the section still shows the second display selected
