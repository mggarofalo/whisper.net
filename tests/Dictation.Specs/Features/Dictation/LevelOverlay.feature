# Coverage map (acceptance criterion -> scenario / test):
#  AC1 overlay appears while recording, hides when the dictation finishes (returns to idle; it now stays
#      visible through the transcribing step, WHISPER-102) -> both scenarios (IsVisible)
#  AC2 live mic-level indicator from pipeline audio, smoothed       -> "Overlay appears... reflects input level" + unit smoothing test
#  AC3 MVVM view-model has no WPF/Infra deps, unit-testable         -> LevelOverlayController (Logic) drives the scenarios + unit tests
#  AC4 show/hide wired to recording state, never stalls pipeline    -> controller observes RecordingStateMachine; level math only reads frames

@WHISPER-26
Feature: Live recording level overlay
  As someone dictating
  I want a small overlay that appears while recording and shows my microphone level
  So that I get a live visual cue that the app is hearing me

  Scenario: Overlay appears while recording and reflects input level
    Given dictation is idle and the overlay is hidden
    When recording starts
    Then the level overlay becomes visible
    And it reflects the current microphone input level

  Scenario: Overlay hides when the dictation finishes
    Given the level overlay is visible while recording
    When the dictation finishes
    Then the level overlay is hidden
