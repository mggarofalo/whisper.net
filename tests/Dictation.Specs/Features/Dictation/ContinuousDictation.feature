# Coverage map (acceptance criterion -> scenario / test):
#  AC1 mode entered/exited via a signal; orchestrator tracks active -> both scenarios + unit tests
#  AC2 after delivery, recording auto-restarts in continuous mode   -> "Recording auto-restarts after each delivery"
#  AC3 Esc exits: pipeline returns to idle, no auto-restart         -> "Esc exits continuous mode"
#  AC4 off => single-shot (no regression)                          -> unit "Single-shot when continuous mode is off" + WHISPER-14 scenarios
#  AC5 each entry/exit/restart logged; loop cannot spin            -> unit tests assert bounded restart + logs

@WHISPER-28
Feature: Continuous dictation mode
  As someone dictating a long passage hands-free
  I want recording to restart automatically after each utterance until I press Esc
  So that I can keep speaking without re-triggering the hotkey each time

  Scenario: Recording auto-restarts after each delivery
    Given continuous dictation mode is active
    When an utterance is transcribed and delivered
    Then recording restarts automatically for the next utterance

  Scenario: Esc exits continuous mode
    Given continuous dictation mode is active
    When the user presses Esc to exit
    Then recording does not restart
    And continuous dictation returns to idle
