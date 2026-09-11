# WHISPER-140 acceptance coverage:
#  AC1, AC3 -> "A dictation survives a crashed GPU worker"
#  AC4      -> "Recovery falls back when GPU inference remains unavailable"
#  AC5      -> "Repeated worker failure is bounded"
#  AC2      -> "A healthy worker keeps its warmed model"
#  AC6      -> "Shutting down cleans up the inference worker"
#  AC7-AC10 are adapter, packaging, logging, privacy, and architecture contracts covered by
#  focused xUnit/architecture/packaging tests; they do not add conversation value as Gherkin.

Feature: Recover dictation by replacing the isolated inference worker
  As someone relying on uninterrupted local dictation
  I want native inference isolated from the tray application
  So a graphics-driver reset cannot take dictation or the tray application down with it

  @WHISPER-140
  Scenario: A dictation survives a crashed GPU worker
    Given a warmed GPU inference worker
    And the inference worker becomes unavailable during transcription
    When dictation is requested through the worker supervisor
    Then a fresh GPU inference worker retries the dictation
    And the transcription result is returned
    And the tray-side supervisor remains available

  @WHISPER-140
  Scenario: Recovery falls back when GPU inference remains unavailable
    Given GPU inference becomes unavailable during transcription
    And a fresh GPU inference worker also cannot complete inference
    When dictation is requested through the worker supervisor
    Then a CPU-only inference worker retries the dictation
    And the transcription result is returned

  @WHISPER-140
  Scenario: Repeated worker failure is bounded
    Given GPU recovery and CPU fallback both fail
    When dictation is requested through the worker supervisor
    Then an inference-unavailable error is returned
    And no further worker restart is attempted for that request
    And the tray-side supervisor remains available

  @WHISPER-140
  Scenario: A healthy worker keeps its warmed model
    Given a healthy warmed GPU inference worker
    When two dictations are requested through the worker supervisor
    Then both transcription results are returned by the same worker generation

  @WHISPER-140
  Scenario: Shutting down cleans up the inference worker
    Given the tray application owns a running inference worker
    When the worker supervisor shuts down
    Then the inference worker exits
    And no owned inference worker remains
