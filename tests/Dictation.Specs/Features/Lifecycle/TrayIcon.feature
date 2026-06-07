@WHISPER-18
Feature: Tray icon and context menu
  Because the app launches to tray with no window, the tray icon is the user's primary entry point. It
  reflects the current dictation status, and its context menu opens settings and quits the app — quit
  triggering a graceful shutdown.

  Scenario: Tray icon reflects dictation status
    Given the application is running in the tray
    When the dictation status changes to recording
    Then the tray icon updates to the recording indicator
    And the tray tooltip describes the recording status

  Scenario: Quit from the tray menu shuts down the app
    Given the tray context menu is open
    When the user selects "Quit"
    Then the application shuts down gracefully

  Scenario: Open Settings from the tray menu shows the settings window
    Given the application is running in the tray
    When the user selects "Open Settings"
    Then the settings window is shown
