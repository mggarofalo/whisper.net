# Coverage map (acceptance criterion -> scenario / test):
#  AC1 audio view lists input devices + shows current selection -> "Selecting an input device persists" (devices listed in Given/Then)
#  AC2 hotkey view assigns a binding + reflects current         -> "Assigning a hotkey persists"
#  AC3 a change dispatches UpdateSettings + persists on reload   -> both "persists" scenarios (reopen/after reload)
#  AC4 invalid hotkey rejected via validation, settings unchanged -> "An invalid hotkey is rejected"

@WHISPER-33
Feature: Audio and hotkey configuration
  As someone setting up dictation
  I want to choose my microphone and dictation hotkey
  So that recording uses the right device and key, and my choices stick

  Scenario: Selecting an input device persists
    Given the audio settings view shows the available input devices
    When the user selects the "Mic-B" input device
    Then an update-settings request is dispatched
    And "Mic-B" is still selected when the view is reopened

  Scenario: Assigning a hotkey persists
    Given the hotkey settings view shows the current binding
    When the user assigns the valid hotkey "Ctrl+Win"
    Then an update-settings request is dispatched
    And the binding "Ctrl+Win" is shown after reload

  Scenario: An invalid hotkey is rejected
    Given the hotkey settings view shows the current binding
    When the user assigns an empty hotkey
    Then the hotkey change is rejected and surfaced
    And no settings are written
