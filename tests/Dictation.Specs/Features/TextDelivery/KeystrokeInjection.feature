@WHISPER-2
Feature: Universal keystroke text delivery
  As someone dictating into any application
  I want transcribed text typed as Unicode keystrokes
  So that it lands in the focused field even where clipboard paste is unreliable

  Scenario: Deliver Unicode text into the focused window by typing
    Given a window has keyboard focus
    And a transcription result "café ✓" is ready for delivery
    When the text injector delivers the result
    Then the focused window receives the exact characters "café ✓"

  Scenario: Non-BMP characters are typed correctly
    Given a window has keyboard focus
    And a transcription result "ship it 🚀" is ready for delivery
    When the text injector delivers the result
    Then the focused window receives the exact characters "ship it 🚀"

  Scenario: Delivery succeeds where clipboard paste is unreliable
    Given a terminal that ignores the standard paste shortcut has focus
    And a transcription result "git status" is ready for delivery
    When the text injector delivers the result
    Then the focused window receives the exact characters "git status"
