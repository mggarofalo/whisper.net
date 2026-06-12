@WHISPER-129
Feature: Model warm-up status
  As someone who just launched the app (or switched models)
  I want to see that the model is warming up and have that cue clear on its own
  So that the first-dictation pause is explained and nothing is left looking stuck

  Scenario: One app-wide event lights every surface, and another clears them all
    Given the Home dashboard is open and the model is not warming up
    When the model begins warming up
    Then the dictation overlay shows the warming state
    And the Home dashboard shows the warming status
    When the model finishes warming up
    Then the dictation overlay is hidden
    And the Home dashboard no longer shows the warming status
