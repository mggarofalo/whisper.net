@WHISPER-12
Feature: Launch to tray with no startup window
  The tray-resident dictation app is owned by a Generic Host. Launching starts every long-lived
  background component as a hosted service and shows no window — the process runs to tray. A graceful
  shutdown stops every hosted service before the process exits.

  Scenario: Launching starts every hosted service with no startup window
    Given the application host is composed with its background components
    When the application is launched
    Then every hosted service has been started
    And the global hotkey listener is observing
    And the application is running tray-resident with no window shown

  Scenario: Shutdown stops every hosted service before the process exits
    Given the application host is composed with its background components
    And the application has been launched
    When application shutdown is requested
    Then every hosted service has been stopped before the host exits
    And the global hotkey listener has stopped observing
