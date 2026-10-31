# Coverage map (acceptance criterion -> scenario):
#  AC1 3 s with no input above the trimmer's threshold shows the hint -> "A recording that stays silent...",
#                                                                        "Background noise below..."
#  AC2 the hint clears as soon as input crosses the threshold          -> "A quiet room that turns into speech...",
#                                                                        "Speech before 3 seconds..."
#  AC3 a recording that ends with no audio says so in the overlay      -> "A recording that ends with no audio..."
#                                                                        + EndToEndDictation "A silent recording tells..."
#  AC4 specs                                                           -> this feature
# The on-screen styling of the message is the manual remainder (smoke-built in OverlayViewSmokeTests).

@WHISPER-144
Feature: The overlay tells me when the microphone isn't picking anything up
  As someone dictating
  I want the overlay to tell me early when nothing is reaching the microphone
  So that a muted mic is obvious while I'm still talking, not after I've given up

  Scenario: A recording that stays silent for 3 seconds shows the muted-mic hint
    Given a dictation recording has started
    When 3 seconds of silent recording elapse
    Then the overlay says "No audio — is your mic muted?"

  Scenario: Background noise below the speech threshold still shows the hint
    Given a dictation recording has started
    When quiet background noise is captured for 3 seconds
    Then the overlay says "No audio — is your mic muted?"

  Scenario: A quiet room that turns into speech clears the hint
    Given a dictation recording has started
    And 3 seconds of silent recording elapse
    When the user starts speaking
    Then the overlay shows no message
    And the overlay shows the recording state

  Scenario: Speech before 3 seconds means the hint never appears
    Given a dictation recording has started
    And the user starts speaking
    When 3 seconds of silent recording elapse
    Then the overlay shows no message

  Scenario: A recording that ends with no audio says so in the overlay
    Given a dictation recording has started
    When the recording ends with no audio captured
    Then the overlay says "No audio detected — is your mic muted?"
    And the overlay is still visible
    When 4 seconds of recording elapse
    Then the overlay shows no message
    And the overlay is hidden

  Scenario: The next recording starts without a stale hint
    Given a dictation recording has started
    And 3 seconds of silent recording elapse
    And the recording finishes and the overlay returns to rest
    When a dictation recording starts
    Then the overlay shows no message
