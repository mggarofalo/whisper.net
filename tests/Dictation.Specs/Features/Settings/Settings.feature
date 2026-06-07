@WHISPER-46
Feature: Settings are queried and updated through Mediator
  As the dictation app
  I want settings read and changed through the Mediator pipeline
  So that reads are projected to DTOs and writes are validated before they persist

  # AC: GetSettingsQuery loads via ISettingsStore and returns the current settings.
  Scenario: Current settings are returned
    Given the settings store holds the user's saved settings
    When the current settings are requested
    Then the saved settings are returned to the caller

  # AC: a valid UpdateSettingsCommand persists the change via ISettingsStore.
  Scenario: A valid settings update is persisted
    Given a valid settings update
    When the settings update is submitted
    Then the new settings are written to the settings store

  # AC: an invalid update is short-circuited by the pipeline before the handler / persistence.
  Scenario: An invalid settings update is rejected before persistence
    Given a settings update with an unknown model id
    When the settings update is submitted
    Then the update is rejected and nothing is written to the settings store
