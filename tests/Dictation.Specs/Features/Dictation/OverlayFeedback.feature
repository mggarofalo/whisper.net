# Coverage map (acceptance criterion -> scenario / test):
#  AC1 user can tell recording / transcribing / errored at a glance
#         -> "...recording state...", "...transcribing state on stop", "...error when dictation fails"
#  AC2 elapsed time visible while recording
#         -> "...ticks elapsed time while recording"
#  AC3 near-cap warning before any audio is dropped
#         -> "...warns when the recording nears the cap"
#  AC4 overlay footprint unchanged + AC5 STA smoke coverage
#         -> Presentation.Smoke.Tests/OverlayViewSmokeTests (constructs the overlay, binds it, pins its
#            compact footprint); on-screen styling is the manual remainder

@WHISPER-102
Feature: The recording overlay communicates state, elapsed time, and a near-cap warning
  As someone dictating
  I want the overlay to show whether it is recording, transcribing, or errored, how long it has run,
  and a warning before it hits the duration cap
  So that I am never left guessing what the app is doing

  Scenario: The overlay shows the recording state and ticks elapsed time while recording
    Given a dictation recording has started
    When 3 seconds of recording elapse
    Then the overlay shows the recording state
    And the overlay elapsed time is at least 3 seconds

  Scenario: The overlay switches to the transcribing state on stop
    Given a dictation recording has started
    When recording stops for transcription
    Then the overlay shows the transcribing state
    And the overlay is still visible

  Scenario: The overlay warns when the recording nears the cap
    Given a dictation recording has started
    When the recording nears the duration cap
    Then the overlay shows the near-cap warning

  Scenario: The overlay shows an error when dictation fails
    Given a dictation recording has started
    When the dictation fails
    Then the overlay shows the error state
    And the overlay is still visible
