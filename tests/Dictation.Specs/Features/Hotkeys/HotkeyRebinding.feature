# Coverage map (acceptance criterion -> scenario):
#  AC1 one-shot capture resolves the first complete chord, then exits -> "Capture and apply a new binding"
#  AC2 captured input maps to the binding model (incl. F13/extended)  -> "Capture and apply ..." (Ctrl+Alt+R);
#                                                                        FromKeys/F13 covered in unit tests
#  AC3 rebind applies atomically; old binding no longer triggers       -> "Capture and apply ..." (new chord fires);
#                                                                        old-stops-firing in unit tests
#  AC4 invalid capture (bare modifier) rejected; previous kept         -> "A bare modifier is rejected ..."
#  AC5 capture can be cancelled (Esc); current binding unchanged       -> "Capture can be cancelled with Esc"

@WHISPER-30
Feature: Hotkey rebinding
  As someone configuring the dictation hotkey
  I want to press a new chord to rebind it, with bad captures rejected
  So that I can change the hotkey without editing config by hand

  Scenario: Capture and apply a new binding
    Given hotkey capture has started
    When the chord "Ctrl+Alt+R" is captured
    Then capture resolves to the binding "Ctrl+Alt+R"
    And holding "Ctrl+Alt+R" triggers recording

  Scenario: A bare modifier is rejected and the previous binding is kept
    Given hotkey capture has started
    When only "Ctrl" is pressed and released
    Then the capture is rejected
    And holding "Ctrl+Win" still triggers recording

  Scenario: Capture can be cancelled with Esc
    Given hotkey capture has started
    When "Esc" is pressed during capture
    Then the capture is cancelled
    And holding "Ctrl+Win" still triggers recording
