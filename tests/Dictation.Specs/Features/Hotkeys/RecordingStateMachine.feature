# Coverage map (acceptance criterion -> scenario):
#  AC1 explicit states Idle/Recording/Transcribing + transitions -> "Happy-path capture cycle"
#  AC2 start->Recording, stop->Transcribing, complete->Idle       -> "Happy-path capture cycle"
#  AC3 Esc cancels: capture discarded, no text delivered, -> Idle -> "Esc cancels an in-flight recording"
#  AC4 illegal transitions are no-ops, not error states           -> "A start request while recording is ignored"
#  AC5 state changes are observable                               -> driver subscribes to StateChanged (every scenario)

@WHISPER-22
Feature: Recording state machine
  As the dictation pipeline
  I want one authoritative recording state with an Esc cancel
  So that captures never overlap and a cancelled capture is never delivered

  Scenario: Happy-path capture cycle
    Given the recorder is Idle
    When a start request is received
    Then the recorder is Recording
    When a stop request is received
    Then the recorder is Transcribing
    When transcription completes
    Then the recorder is Idle

  Scenario: Esc cancels an in-flight recording
    Given the recorder is Recording
    When the user presses Esc
    Then the capture is discarded
    And no text is delivered
    And the recorder is Idle

  Scenario: A start request while already recording is ignored
    Given the recorder is Recording
    When a start request is received
    Then the recorder is Recording
