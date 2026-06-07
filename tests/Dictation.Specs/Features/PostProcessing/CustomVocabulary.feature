# Coverage map (acceptance criterion -> scenario):
#  AC1 vocabulary assembled into a biasing initial prompt        -> "Bias the decoder toward user terms"
#  AC2 non-empty vocab disables the first-token threshold        -> "Bias the decoder toward user terms"
#  AC3 empty vocab leaves prompt + threshold untouched           -> "No vocabulary leaves decoding unchanged"
#  AC4 changes take effect next transcription, no engine restart -> "Vocabulary changes take effect on the next transcription"
#  AC5 assembly unit-tested in isolation (no native load)        -> VocabularyConditioner unit tests

@WHISPER-38
Feature: Custom vocabulary conditioning
  A user-supplied vocabulary biases the Whisper decoder toward domain terms via prompt-token
  conditioning. When a vocabulary prompt is present the first-token log-probability threshold is
  disabled (an injected prompt can otherwise drop the genuine first token); an empty vocabulary leaves
  decoding untouched. Changes apply on the next transcription without reloading the model.

  Scenario: Bias the decoder toward user terms
    Given a custom vocabulary containing "Reqnroll" and "Velopack"
    When transcription decoding options are assembled
    Then the initial prompt includes those terms
    And the first-token log-probability threshold is disabled

  Scenario: No vocabulary leaves decoding unchanged
    Given an empty custom vocabulary
    When transcription decoding options are assembled
    Then no initial prompt is set
    And the first-token log-probability threshold retains its default

  Scenario: Vocabulary changes take effect on the next transcription
    Given a loaded transcriber with the custom vocabulary "Reqnroll"
    When a clip is transcribed
    Then the decoder was conditioned with a prompt containing "Reqnroll"
    When the custom vocabulary changes to "Velopack"
    And a clip is transcribed
    Then the decoder was conditioned with a prompt containing "Velopack"
    And the engine was loaded only once
