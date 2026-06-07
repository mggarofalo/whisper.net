# Coverage map (acceptance criterion -> scenario):
#  AC1 IHotkeyListener port: key-down/key-up over domain types -> every scenario drives the port;
#                                                                 no-leakage guarded by @WHISPER-44
#  AC4 raw OS key events translated to domain key + modifiers  -> "Key events are observed ...",
#                                                                 "Modifier-only chords ...",
#                                                                 "Releasing a key clears ..."
#  AC3 dedicated thread; non-blocking start; clean dispose+join -> "Listener shuts down cleanly"
#                                                                 (+ thread/join unit-tested)
#  AC2 registered in Infrastructure DI                          -> Hosting.Tests host-composition (see PR)
#  AC5 hook-start failure logged, host survives                 -> EventLoopHotkeyListener unit test (see PR)

@WHISPER-10
Feature: Global hotkey listening
  As the dictation app
  I want global key edges observed regardless of which window has focus
  So that push-to-talk, chords, and F13 can drive recording from anywhere

  Scenario: Key events are observed with the corresponding domain key and modifiers
    Given the global hotkey listener is started
    When the chord "Ctrl+F13" is pressed at the OS hook
    Then a key-down is observed for "F13" with modifiers "Ctrl"

  Scenario: A modifier-only chord reports the full active modifier set
    Given the global hotkey listener is started
    When the chord "Ctrl+Win" is pressed at the OS hook
    Then a key-down is observed for "Win" with modifiers "Ctrl+Win"

  Scenario: Releasing a key clears its modifier from the active set
    Given the global hotkey listener is started
    When the key "Ctrl" is pressed at the OS hook
    And the key "Ctrl" is released at the OS hook
    Then a key-up is observed for "Ctrl" with modifiers "None"

  Scenario: Listener shuts down cleanly
    Given the global hotkey listener is started
    When the listener is disposed
    Then the hook event loop has stopped
    And no further key events are observed
