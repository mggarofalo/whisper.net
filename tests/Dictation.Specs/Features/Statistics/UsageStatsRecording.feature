@WHISPER-24
Feature: Usage statistics recording and aggregation
  Each completed transcription records its character count and the captured audio duration, persisted so
  the totals survive a restart. A usage-summary query aggregates them into the transcription count, total
  characters, and total audio duration. Exercised end-to-end through Mediator over a real temp SQLite DB.

  # AC: aggregates correctly sum across several recorded transcriptions.
  Scenario: Aggregates reflect recorded transcriptions
    Given no statistics have been recorded
    When I record a transcription of 12 seconds with 80 characters
    And I record a transcription of 8 seconds with 40 characters
    Then the total transcription count is 2
    And the total audio duration is 20 seconds
    And the total character count is 120

  # AC: recorded measures are persisted via the store, so they survive a restart.
  Scenario: Recorded statistics survive a restart
    Given I have recorded a transcription of 5 seconds with 30 characters
    When the statistics store is reopened
    Then the total transcription count is 1
    And the total audio duration is 5 seconds
    And the total character count is 30
