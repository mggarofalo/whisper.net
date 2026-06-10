# Coverage map (acceptance criterion -> scenario):
#  AC1 frames -> 16 kHz mono float regardless of source -> "Source audio is normalized ..." (outline)
#  AC2 ring buffer retains the most recent preroll       -> "Preroll captures speech before the trigger"
#  AC3 preroll prepended on record start                 -> "Preroll captures speech before the trigger"
#  AC4 max-duration limit is observable (revised by      -> "The maximum duration is a soft limit the recording grows past"
#      WHISPER-111: the limit is soft, nothing is dropped)
#  AC5 allocation-conscious (reused, capacity-hinted)    -> CaptureBuffer unit tests (recording reuse, ring eviction)
#  AC6 platform-agnostic, synthetic-frame testable       -> the whole feature runs with no device

@WHISPER-23
Feature: Captured audio is normalized and buffered for transcription
  As the dictation pipeline
  I want device audio resampled to 16 kHz mono with a preroll and an observable duration limit
  So that recordings are in the format Whisper expects, never clip speech onset, and never lose audio

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

  # WHISPER-111 revised the cap into a soft limit: whisper.cpp handles arbitrary-length clips, so the
  # buffer keeps growing past the limit and the limit event merely makes the overrun observable.
  @WHISPER-111
  Scenario: The maximum duration is a soft limit the recording grows past
    Given a maximum recording duration of 2000 ms
    And recording has started
    When 2500 ms of audio is captured
    Then the finalized recording is exactly 2500 ms long
    And the maximum-duration limit is reported to the caller
