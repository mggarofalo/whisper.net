# Coverage map (acceptance criterion -> scenario):
#  AC1 activating the hotkey section loads and shows the persisted binding (never "(none)")
#         -> "Activating the hotkey section shows the current binding"
#  AC2 assigning a hotkey persists it and rebinds the live matcher immediately (no restart)
#         -> "Assigning a hotkey takes effect immediately without a restart"
#  AC3 the assigned chord is the one startup registers on the next launch
#         -> "The assigned hotkey is the chord registered on the next launch"
#
# The defect (WHISPER-109): activation registered the messenger but never loaded the settings, so the
# section showed no binding and Assign silently no-opped on its null-settings guard. These scenarios
# therefore enter through the REAL navigation lifecycle (OnNavigatedTo), never LoadCommand directly.

@WHISPER-109
Feature: Hotkey assignment from the settings section takes effect
  As a user assigning a new dictation hotkey
  I want the section to show my current binding and apply my new one immediately
  So that assigning a hotkey is never a silent no-op

  Scenario: Activating the hotkey section shows the current binding
    Given settings persisted with the hotkey "Ctrl+Win"
    When the user opens the hotkey section
    Then the hotkey section shows the current binding "Ctrl+Win"

  Scenario: Assigning a hotkey takes effect immediately without a restart
    Given settings persisted with the hotkey "Ctrl+Win"
    And the hotkey pipeline has started
    And the hotkey section is open
    When the user captures and assigns the hotkey "F13"
    Then the live matcher is bound to "F13"
    And the live matcher is no longer bound to "Ctrl+Win"
    And the persisted settings hold the hotkey "F13"

  Scenario: The assigned hotkey is the chord registered on the next launch
    Given settings persisted with the hotkey "Ctrl+Win"
    And the hotkey pipeline has started
    And the hotkey section is open
    When the user captures and assigns the hotkey "F13"
    And the application is relaunched
    Then the live matcher is bound to "F13"
