# Coverage map (acceptance criterion -> scenario):
#  AC1 explicit pipeline state Idle->Recording->Transcribing->Delivering->Idle -> both scenarios (stage returns to Idle)
#  AC2 start begins capture; stop moves to transcribing                        -> "A spoken phrase is captured, transcribed, and delivered"
#  AC3 on stop: transcribe via Mediator then deliver, no manual step           -> "A spoken phrase is captured, transcribed, and delivered"
#  AC4 a stage error returns the pipeline to a safe Idle and is logged         -> "A transcription failure returns the pipeline to a safe state"
#  AC5 port-only composition, fakeable                                         -> whole feature runs over faked Infrastructure ports
#  AC6 transitions/durations logged structurally                              -> "the failure is logged" + unit coverage in Logic.AppManagement.Tests

@WHISPER-14
Feature: End-to-end dictation orchestration
  As someone dictating into any application
  I want one orchestrator to run capture -> transcribe -> deliver
  So that a single hotkey turns my speech into text with no manual steps

  Scenario: A spoken phrase is captured, transcribed, and delivered
    Given the dictation pipeline is idle
    And the model will transcribe the captured audio to "book the flight for friday"
    When the user starts dictation, speaks, and stops
    Then the captured audio is transcribed
    And the text delivered to the active application is "book the flight for friday"
    And the dictation pipeline returns to idle

  Scenario: A transcription failure returns the pipeline to a safe state
    Given the dictation pipeline is recording
    When transcription fails
    Then the dictation failure is logged
    And no text is delivered to the active application
    And the dictation pipeline returns to idle
