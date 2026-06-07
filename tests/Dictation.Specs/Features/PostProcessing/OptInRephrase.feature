# Coverage map (acceptance criterion -> scenario):
#  AC1 IRephraseClient port + Ollama impl (DTOs in/out)       -> all scenarios (real client behind the port)
#  AC2 disabled by default makes no network call              -> "Disabled by default makes no network call"
#  AC3 enabled requires loopback; remote host rejected        -> "Non-loopback endpoint is rejected"
#  AC4 opt-in + localhost-only disclosed in README + CHANGELOG -> docs (README + CHANGELOG)
#  AC5 backend failures are recoverable, never crash          -> "A rephrase backend failure degrades to the original text"

@WHISPER-40
Feature: Opt-in localhost rephrase
  AI rephrase is the single transcript-bearing network seam, so it is disabled by default, only ever
  talks to a loopback host, and never crashes the pipeline when the local model is unavailable.

  Scenario: Disabled by default makes no network call
    Given the AI rephrase setting has never been enabled
    When text is sent for rephrasing
    Then no rephrase request is sent
    And a "rephrase disabled" result is returned

  Scenario: Non-loopback endpoint is rejected
    Given AI rephrase is enabled
    And the configured endpoint host is "ollama.example.com"
    When the rephrase configuration is validated
    Then validation fails with a "localhost only" error
    And no rephrase request is sent

  Scenario: Enabled localhost rephrase rewrites the text
    Given AI rephrase is enabled against a local Ollama returning "polished text"
    When the text "rough text" is sent for rephrasing
    Then the request goes to a loopback endpoint
    And the rewritten text "polished text" is returned

  Scenario: A rephrase backend failure degrades to the original text
    Given AI rephrase is enabled against a failing local Ollama
    When the text "keep me" is sent for rephrasing
    Then the original text "keep me" is returned as a recoverable failure
