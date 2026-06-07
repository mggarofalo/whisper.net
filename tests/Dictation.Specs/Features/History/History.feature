@WHISPER-47
Feature: Transcription history is recorded and queried via Mediator
  As the dictation app
  I want completed transcriptions recorded and read back through Mediator
  So that the pipeline stays decoupled from how history is stored

  # AC: RecordTranscriptionCommand persists a matching TranscriptEntry via IHistoryStore.
  Scenario: A completed transcription is recorded
    Given a completed transcription with text "take notes"
    When the transcription is recorded
    Then a matching transcript entry is saved in the history store

  # AC: QueryHistoryQuery returns history newest-first, honoring the limit.
  Scenario: History is returned newest-first
    Given the history store contains three transcript entries from different times
    When the history is queried with a limit of two
    Then the two most recent entries are returned newest-first
