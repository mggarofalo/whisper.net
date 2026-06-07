@WHISPER-5
Feature: Clipboard fallback delivery without clobbering newer content
  As someone whose clipboard holds things I care about
  I want paste-based delivery to restore what I had copied
  So that dictating never silently destroys my clipboard

  Scenario: Original clipboard is restored after pasting
    Given the clipboard contains user content "important note"
    And no other process modifies the clipboard during delivery
    When the text "meeting at noon" is delivered via the clipboard path
    Then the delivered text "meeting at noon" is pasted into the focused window
    And the clipboard again contains "important note"

  Scenario: Restore is skipped when newer content arrives
    Given the clipboard contains user content "old"
    When the text "meeting at noon" is delivered via the clipboard path
    And another process copies "new" before restore occurs
    Then the clipboard still contains "new"
