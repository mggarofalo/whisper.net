# Coverage map (acceptance criterion -> scenario):
#  AC1 frames -> 16 kHz mono float regardless of source -> "Source audio is normalized ..." (outline)
#  AC2 ring buffer retains the most recent preroll       -> "Preroll captures speech before the trigger"
#  AC3 preroll prepended on record start                 -> "Preroll captures speech before the trigger"
#  AC4 max-duration cap finalizes + is observable        -> "Recording is capped at the maximum duration"
#  AC5 allocation-conscious (reused, bounded buffers)    -> CaptureBuffer unit tests (bounded recording, ring eviction)
#  AC6 platform-agnostic, synthetic-frame testable       -> the whole feature runs with no device

@WHISPER-23
Feature: Captured audio is normalized and buffered for transcription
  As the dictation pipeline
  I want device audio resampled to 16 kHz mono with a preroll and a duration cap
  So that recordings are in the format Whisper expects, never clip speech onset, and stay bounded

  Scenario Outline: Source audio is normalized to the Whisper format
    Given one second of source audio at <rate> Hz with <channels> channel(s)
    When the audio is normalized
    Then the result is one second of 16000 Hz mono float audio

    Examples:
      | rate  | channels |
      | 44100 | 2        |
      | 48000 | 1        |
      | 16000 | 2        |

  Scenario: Preroll captures speech before the trigger
    Given a buffering preroll of 300 ms
    And 1000 ms of audio has been captured while idle
    When recording starts and then stops with no further audio
    Then the finalized recording is the most recent 300 ms of preroll audio

  Scenario: Recording is capped at the maximum duration
    Given a maximum recording duration of 2000 ms
    And recording has started
    When 2500 ms of audio is captured
    Then the finalized recording is exactly 2000 ms long
    And the maximum-duration cap is reported to the caller
