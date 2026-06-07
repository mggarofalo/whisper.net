@WHISPER-6
Feature: UIPI / elevation-aware delivery
  As someone dictating from an unelevated app
  I want to be told when the focused window is elevated
  So that delivery never silently disappears into a window Windows blocks us from

  Scenario: Delivery into an elevated window is surfaced, not silently dropped
    Given the focused window belongs to a higher-integrity process
    And the application is running unelevated
    And the model will transcribe the audio to "deploy to production"
    When text delivery is attempted
    Then the user is informed delivery was blocked by UIPI
    And no exception is thrown

  Scenario: Delivery proceeds normally into a same-integrity window
    Given the focused window belongs to a same-integrity process
    And the model will transcribe the audio to "deploy to production"
    When text delivery is attempted
    Then the text "deploy to production" is delivered without a UIPI warning
