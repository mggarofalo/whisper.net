@WHISPER-42
Feature: Domain invariants protect the dictation model
  As the dependency-free core of the dictation utility
  I want invalid states rejected at construction
  So that every other layer can trust the values it is handed

  # AC: a RecordingSession cannot end before it starts.
  Scenario: A recording session cannot end before it starts
    Given a recording session started at "10:00:00"
    When the session is ended at "09:59:59"
    Then the domain rejects the operation as an invariant violation

  # AC: a TranscriptEntry cannot have empty text.
  Scenario: A transcript entry requires non-empty recognized text
    When a transcript entry is created with empty recognized text
    Then the domain rejects the entry as an invariant violation

  # AC: UsageStats totals are non-negative.
  Scenario Outline: Usage statistics can never be negative
    When usage statistics are created with <words> words across <sessions> sessions
    Then construction <outcome>

    Examples:
      | words | sessions | outcome     |
      | 100   | 5        | succeeds    |
      | -1    | 5        | is rejected |
      | 100   | -2       | is rejected |
