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
#  WHISPER-143 AC1 every dictation re-presents the overlay, even when already visible
#         -> "...starts while the warming pill is up...", "Every dictation asks...", "A dictation failure re-presents..."
#  WHISPER-143 AC2 desktop move + top-most re-assert on present and while up
#         -> Presentation/Overlay/LevelOverlay (native; smoke-run by OverlayViewSmokeTests), manual on-screen check
#  WHISPER-143 AC3 the real on-screen state is verified, logged, and retried once
#         -> "The overlay's on-screen check names what is wrong" (the verdict) + LevelOverlay (the read-back)

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

  # WHISPER-143: a dictation started while the "Warming up…" pill was already up never re-ran the
  # window's show path (visibility didn't change), so the overlay wasn't re-placed or forced on top and
  # the user saw nothing. Every time a dictation takes over the pill, the overlay is now asked to present
  # itself explicitly, whether or not it was already visible.
  @WHISPER-143
  Scenario: A dictation that starts while the warming pill is up re-presents the overlay
    Given the model is warming up so the overlay shows the warming pill
    When a dictation recording starts
    Then the overlay is asked to present itself on screen
    And the overlay shows the recording state

  @WHISPER-143
  Scenario: Every dictation asks the overlay to present itself
    Given a dictation recording has started
    And the recording finishes and the overlay returns to rest
    When a dictation recording starts
    Then the overlay has been asked to present itself 2 times

  @WHISPER-143
  Scenario: A dictation failure re-presents the overlay
    Given a dictation recording has started
    When the dictation fails
    Then the overlay is asked to present itself on screen

  # WHISPER-143: "Overlay shown" was logged while the user saw nothing. After every present the window
  # reads back its real on-screen state, and only a window that is visible, uncloaked, on the current
  # virtual desktop, top-most and uncovered counts as on screen; anything else is logged and retried once.
  @WHISPER-143
  Scenario Outline: The overlay's on-screen check names what is wrong
    Given the overlay window reads back as <condition>
    Then the overlay is judged <verdict>
    And the on-screen check reports "<reason>"

    Examples:
      | condition                       | verdict       | reason                        |
      | fully on screen                 | on screen     | on screen                     |
      | not visible                     | not on screen | not visible                   |
      | cloaked                         | not on screen | cloaked                       |
      | on another virtual desktop      | not on screen | on another virtual desktop    |
      | not top-most                    | not on screen | not top-most                  |
      | covered by "Notepad"            | not on screen | covered by Notepad            |
