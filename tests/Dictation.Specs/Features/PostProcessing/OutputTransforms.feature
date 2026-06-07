# Coverage map (acceptance criterion -> scenario):
#  AC1 transform abstraction (name/description/prompt) resolved by name -> all scenarios (registry-backed)
#  AC2 built-in bullets/prompt-engineer/polish with ported prompts      -> "Apply the bullets transform" + registry unit tests
#  AC3 apply composes prompt+text via the port; unknown name recoverable -> "Apply the bullets transform" + "Unknown transform name"
#  AC4 graceful degrade when no rephrase client enabled                  -> "Rephrase disabled degrades gracefully"
#  AC5 layer-clean: AI calls go through the port, no Infra/net leakage   -> framework lives in Logic.AppManagement over IRephraseClient

@WHISPER-37
Feature: Output transforms
  Named transforms (bullets, prompt-engineer, polish) rewrite recognized text by composing a prompt
  with the text and delegating the AI rewrite to the rephrase port. Unknown names and a disabled/absent
  rephrase backend are recoverable — they never crash the pipeline.

  Scenario: Apply the bullets transform
    Given the "bullets" transform is registered
    And the rephrase client is available
    When I apply "bullets" to "buy milk and eggs and bread"
    Then the rephrase client receives the bullets prompt with that text
    And the rewritten result is returned

  Scenario: Unknown transform name
    Given no transform named "sparkle" is registered
    When I apply "sparkle" to "some text"
    Then a recoverable "unknown transform" error is returned
    And no rephrase call is made

  Scenario: Rephrase disabled degrades gracefully
    Given the "polish" transform is registered
    And the rephrase client is disabled
    When I apply "polish" to "leave me as is"
    Then the text "leave me as is" is returned unchanged
