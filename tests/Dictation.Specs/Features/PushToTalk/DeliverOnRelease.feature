@WHISPER-58
Feature: Deliver transcription on push-to-talk release
  As someone dictating into any application
  I want the recognized text inserted into the field I'm typing in
  So that I can speak instead of type

  Scenario: Spoken phrase is delivered to the focused field
    Given the model will transcribe the audio to "schedule the meeting for friday"
    When push-to-talk is released
    Then the text delivered to the focused field is "schedule the meeting for friday"

  Scenario: Nothing is delivered when the audio yields no speech
    Given the model will transcribe the audio to ""
    When push-to-talk is released
    Then no text is delivered to the focused field
