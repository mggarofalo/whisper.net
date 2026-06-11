# Coverage map (acceptance criterion -> scenario):
#  AC1 Home shows live, useful status sourced from settings/history
#         -> "The dashboard shows live configuration and usage" + "...a first-class empty state"
#  AC2 the default section on launch is a deliberate choice, not a leftover
#         -> Home is the first registered section and is now a purposeful overview dashboard, so landing
#            on it is deliberate; the registration order is asserted by the existing shell-navigation specs.

@WHISPER-106
Feature: The Home section is a live status dashboard
  As someone using Whisper day to day
  I want the landing section to show my current setup and recent activity at a glance
  So that opening the app gives me a useful overview instead of an empty placeholder

  Scenario: The dashboard shows live configuration and usage from settings and history
    Given the dashboard's settings select model "small.en" and input device "Microphone One"
    And two transcriptions totalling five words have been recorded
    When the Home section is opened
    Then the dashboard shows "small.en" as the active model
    And the dashboard shows "Microphone One" as the input device
    And the dashboard shows the configured hotkey
    And the dashboard shows 2 transcriptions and 5 words
    And the dashboard lists 2 recent transcriptions

  Scenario: The dashboard shows a first-class empty state with no history
    Given the dashboard's settings select model "base.en" and the system default input device
    And the dashboard has no recorded transcriptions
    When the Home section is opened
    Then the dashboard shows zero usage totals
    And the dashboard shows its empty recent state

  # WHISPER-119: the dashboard is a live overview — reopening it must re-query, not show a stale snapshot.
  @WHISPER-119
  Scenario: Reopening Home shows transcriptions recorded since it was last open
    Given the dashboard's settings select model "small.en" and the system default input device
    And the dashboard has no recorded transcriptions
    When the Home section is opened
    Then the dashboard shows its empty recent state
    When a transcription "brand new note" is recorded and the Home section is reopened
    Then the dashboard lists 1 recent transcriptions
    And the most recent dashboard transcription is "brand new note"
