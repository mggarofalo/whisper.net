# Coverage map (acceptance criterion -> scenario):
#  AC2 --doctor gains a check that actually initializes the Whisper native runtime
#         -> both scenarios (the "Whisper" check appears in the report and reflects the probe verdict)
#  The packaging fix itself (AC1/AC3/AC4) is verified by running --doctor on the published build (see PR);
#  this feature guards that the doctor REPORTS the native-runtime state so the defect can never silently
#  pass again.

@WHISPER-85
Feature: Whisper native runtime diagnostic
  As a user whose installed app must actually transcribe
  I want the doctor to verify the Whisper native runtime loads
  So that a packaging defect that breaks all transcription is caught, not silently passed

  Scenario: The doctor fails when the Whisper native runtime cannot load
    Given the "Whisper" subsystem is unavailable
    When the diagnostics run
    Then the "Whisper" check reports a failing status

  Scenario: The doctor passes the Whisper check when the native runtime loads
    Given every subsystem is healthy
    When the diagnostics run
    Then the "Whisper" check does not report a failing status
