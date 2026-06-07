# Coverage map (acceptance criterion -> scenario):
#  AC1 binding model: primary key + zero-or-more modifiers, incl. F13/extended -> every scenario's
#                     bindings (F13, Ctrl+Alt+Space, Ctrl+Alt+D); structural parse unit-tested too
#  AC2 push-to-talk: hold requests start, release requests stop -> "Push-to-talk records while held"
#  AC3 toggle: full press starts, next full press stops          -> "Toggle starts and stops ..."
#  AC4 matching needs all modifiers, ignores unrelated keys,      -> "A partial chord ...",
#      partial/extra presses do not trigger                          "An unrelated key ...",
#                                                                    "Holding modifiers but a different key ..."
#  AC5 mode is configurable; one pipeline serves both modes       -> PTT + toggle share the driver/controller

@WHISPER-16
Feature: Hotkey activation modes
  As someone dictating with a configurable hotkey
  I want push-to-talk and toggle activation over the same chord
  So that I can drive recording the way that suits me, including with F13 and chords

  Scenario: Push-to-talk records while the chord is held
    Given push-to-talk mode with the binding "Ctrl+Alt+Space"
    When the chord "Ctrl+Alt+Space" is held
    Then recording start is requested 1 time
    When the chord "Ctrl+Alt+Space" is released
    Then recording stop is requested 1 time

  Scenario Outline: Toggle starts and stops on successive presses
    Given toggle mode with the binding "<binding>"
    When the chord "<binding>" is fully pressed
    Then recording start is requested 1 time
    When the chord "<binding>" is fully pressed
    Then recording stop is requested 1 time

    Examples:
      | binding    |
      | F13        |
      | Ctrl+Alt+D |

  Scenario: A partial chord does not trigger recording
    Given push-to-talk mode with the binding "Ctrl+Alt+Space"
    When the chord "Ctrl+Alt" is held
    Then recording start is requested 0 times

  Scenario: An unrelated key does not trigger recording
    Given push-to-talk mode with the binding "Ctrl+Alt+Space"
    When the key "J" is pressed and released
    Then recording start is requested 0 times

  Scenario: Holding the modifiers but pressing a different key does not trigger
    Given push-to-talk mode with the binding "Ctrl+Alt+Space"
    When the chord "Ctrl+Alt+D" is held
    Then recording start is requested 0 times
