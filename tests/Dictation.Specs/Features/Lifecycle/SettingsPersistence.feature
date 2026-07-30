@WHISPER-43
Feature: Settings persistence across the lifecycle
  User settings are loaded from a persisted store when the host starts and written back on graceful
  shutdown, so a change survives a restart. A first run with no store yields defaults and creates the
  store, and a corrupt store recovers to defaults without crashing.

  Scenario: Settings survive a restart
    Given the user has changed a setting and the application shuts down gracefully
    When the application is started again
    Then the previously saved setting is loaded

  # A launch that changes nothing must not write to the store. When a load falls back to defaults — a
  # store held open by another process at login, or a corrupt one — writing that snapshot back on shutdown
  # silently reset the user's model, hotkey, and capture device.
  Scenario: A launch that changes nothing leaves the stored settings alone
    Given the user has changed a setting and the application shuts down gracefully
    When the application starts and shuts down without changing anything
    And the application is started again
    Then the previously saved setting is loaded

  Scenario: First run produces defaults
    Given no settings store exists
    When the application starts
    Then default settings are loaded
    And a settings store is created

  Scenario: Corrupt store recovers to defaults
    Given the settings store is corrupt
    When the application starts
    Then default settings are loaded
    And the recovery is logged
