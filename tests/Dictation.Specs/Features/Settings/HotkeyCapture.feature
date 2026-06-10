# Coverage map (acceptance criterion -> scenario / test):
#  AC1 captures Ctrl/Alt/Shift/Win + key, displays "Ctrl + Alt + K", ignores modifier-only presses
#         -> "Capturing a full combination" + "A standalone modifier press is ignored" +
#            Logic.AppManagement.Tests/Shell/HotkeyCaptureInterpreterTests
#  AC2 Alt via Key.System/SystemKey; textbox shortcuts/context menu suppressed
#         -> the WPF HotkeyCaptureControl (Presentation glue, smoke): unwraps Key.System and marks the
#            PreviewKeyDown handled so the read-only TextBox never acts on the keystroke
#  AC3 assigning live-reconfigures the matcher; an unregisterable combo shows a validation error, not applied
#         -> "An unregisterable combination is flagged and not applied" + the @WHISPER-78 instant-apply path

@WHISPER-79
Feature: A pressed key combination is captured for the dictation hotkey
  As a user choosing a dictation hotkey
  I want to press the combination instead of typing it
  So that I get exactly the chord I intend, with invalid ones refused

  Scenario: Capturing a full combination records it for display and assignment
    Given the hotkey capture field has loaded the current binding
    When the user presses modifiers "Control,Alt" with the key "K"
    Then the captured hotkey shows "Ctrl + Alt + K"
    And the captured hotkey is valid

  Scenario: A standalone modifier press is ignored
    Given the hotkey capture field has loaded the current binding
    When the user presses modifiers "Control" with the key "None"
    Then nothing is captured

  Scenario: Pressing Escape clears the capture
    Given the hotkey capture field has loaded the current binding
    When the user presses modifiers "Control,Alt" with the key "K"
    And the user presses modifiers "None" with the key "Escape"
    Then nothing is captured

  Scenario: An unregisterable combination is flagged and not applied
    Given the hotkey capture field has loaded the current binding
    When the user presses modifiers "Control" with the key "Unknown"
    And the user assigns the captured hotkey
    Then the captured hotkey reports a validation error
    And the captured hotkey is not persisted
