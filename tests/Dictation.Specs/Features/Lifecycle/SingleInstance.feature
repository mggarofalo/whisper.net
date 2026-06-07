@WHISPER-25
Feature: Single-instance enforcement
  Only one instance may own the audio device, hotkey hooks, and tray icon at a time. On startup the app
  acquires a named lock; a second launch detects the running instance, signals it to surface, and exits
  without starting a second host. The lock is released on graceful shutdown so a later launch becomes the
  sole instance.

  Scenario: Second launch activates the existing instance
    Given an instance of the application is already running
    When the user launches the application again
    Then the second process exits without starting a new instance
    And the existing instance is brought to the foreground

  Scenario: Sole instance after the first exits
    Given a previous instance has shut down gracefully
    When the user launches the application
    Then the application starts as the sole instance
