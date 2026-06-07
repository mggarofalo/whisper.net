@WHISPER-3
Feature: On-device transcription via Whisper.net
  As the dictation pipeline
  I want captured audio turned into text entirely on this device
  So that speech becomes text with no audio ever leaving the machine

  Scenario: Transcribe a known 16 kHz audio clip
    Given a loaded model and a 16 kHz mono PCM clip of "schedule the meeting"
    When the transcriber processes the clip
    Then the recognized text is "schedule the meeting"
    And no network egress occurs during transcription

  Scenario: Missing model file fails gracefully
    Given a model path that does not exist on disk
    When the transcriber processes the clip
    Then a typed model-not-found error is returned
    And the application does not crash
