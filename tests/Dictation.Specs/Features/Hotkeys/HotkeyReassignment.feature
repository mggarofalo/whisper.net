# Coverage map (acceptance criterion -> scenario):
#  AC1 the live matcher is reconfigured immediately when the hotkey setting changes (no restart)
#         -> "A newly assigned hotkey drives the live matcher"
#  AC2 the persisted hotkey is applied at startup (a changed binding survives a restart)
#         -> "The persisted hotkey is applied at startup"
#  AC3 decoupled via a settings-change broadcaster the update handler raises -> exercised by both
#         scenarios going through the real UpdateSettingsCommand pipeline

@WHISPER-75
Feature: Hotkey reassignment takes effect
  As a user who customizes the dictation hotkey
  I want a newly assigned hotkey to actually drive recording
  So that changing the hotkey is not silently ignored

  Scenario: A newly assigned hotkey drives the live matcher
    Given the dictation pipeline has started
    When the dictation hotkey is changed to "Ctrl+Shift+J"
    Then the activation controller matches the chord "Ctrl+Shift+J"

  Scenario: The persisted hotkey is applied at startup
    Given a persisted hotkey "Alt+F9"
    When the dictation pipeline starts
    Then the activation controller matches the chord "Alt+F9"
