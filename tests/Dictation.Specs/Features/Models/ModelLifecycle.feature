@WHISPER-15
Feature: Model lifecycle management
  As the app that owns one expensive, stateful model
  I want models warmed, switched, and released deliberately
  So that the first utterance is fast and switching never leaks a model

  Scenario: Warmup eliminates first-utterance lag
    Given a model has just been loaded with warmup enabled
    When the first transcription is requested
    Then it runs without incurring lazy-initialization latency

  Scenario: Switching models releases the previous one
    Given the "base" model is loaded and ready
    When the user switches to the "small" model
    Then the "small" model becomes the active ready model
    And the "base" model's resources are released
