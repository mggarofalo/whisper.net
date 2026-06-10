# Coverage map (acceptance criterion -> scenario):
#  AC1 a dictation longer than the former cap keeps every spoken sample -> "A dictation longer than the old buffer cap transcribes all spoken audio"
#  AC2 the user is warned approaching the limit, before anything could  -> "Approaching the recording limit publishes a warning before any audio could be lost"
#      be lost, and signalled at it — while recording continues
#  AC3 unit coverage for the growable buffer + soft-limit events        -> CaptureBufferTests (Logic.AudioManagement.Tests)
#                                                                          + DictationOrchestratorTests (Logic.AppManagement.Tests)
#  hard failsafe: a runaway recording stops AND transcribes itself at   -> "An extremely long dictation is stopped and transcribed at the hard limit"
#      the hard ceiling — never discarded

@WHISPER-111
Feature: Long dictation beyond the former buffer cap
  As someone dictating a long passage
  I want the recording to keep growing past the configured duration limit
  So that everything I said reaches the transcriber instead of being silently cut off

  # The 30-second cap was a pure app-layer artifact — whisper.cpp handles arbitrary-length clips
  # internally. The maximum duration is now a SOFT limit: the capture buffer keeps growing past it,
  # and messenger signals warn as the recording approaches and reaches it. Nothing is dropped.
  Scenario: A dictation longer than the old buffer cap transcribes all spoken audio
    Given a recording soft limit of 2000 ms
    And the user dictates past the soft limit
    When the user stops dictating
    Then the clip handed to the transcriber contains the audio spoken before the limit
    And the clip handed to the transcriber contains the audio spoken after the limit

  Scenario: Approaching the recording limit publishes a warning before any audio could be lost
    Given a recording soft limit of 2000 ms
    When the user dictates up to the near-limit threshold
    Then a near-limit warning is published
    When the user keeps dictating past the soft limit
    Then an at-limit signal is published
    And the audio spoken past the limit is still retained in the recording

  # The hard failsafe behind the soft limit: with no UI consuming the warnings yet, the recording
  # cannot be allowed to grow without end. At the hard ceiling the app stops the dictation itself
  # through the NORMAL stop path — everything recorded is transcribed and delivered, never discarded.
  Scenario: An extremely long dictation is stopped and transcribed at the hard limit
    Given a recording soft limit of 1000 ms
    And a recording hard limit of 2000 ms
    When the user dictates past the hard limit
    Then the dictation is stopped and the clip is transcribed automatically
    And a hard-limit stop signal is published
