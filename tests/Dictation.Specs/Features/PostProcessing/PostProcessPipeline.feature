# Coverage map (acceptance criterion -> scenario):
#  AC1 single config section (filler, vocab, default transform, rephrase) -> all scenarios via PostProcessOptions
#  AC2 ordered pipeline normalize -> optional transform, toggleable        -> "Applies the configured default transform after normalizing"
#  AC3 edits picked up on the next transcription without a restart          -> "Hot-reload picks up a changed setting"
#  AC4 invalid config reported clearly + safe fallback (no crash)           -> "An unknown default transform is rejected and the pipeline degrades safely"
#  AC5 config validated via FluentValidation through ValidationBehavior     -> "An unknown default transform is rejected ..." (mediated command)

@WHISPER-41
Feature: Post-process pipeline configuration
  Filler removal, custom vocabulary, the default output transform, and the opt-in rephrase live in one
  configuration section. The pipeline applies normalize then the optional transform, edits take effect
  on the next transcription without a restart, and an invalid configuration is reported (FluentValidation)
  and degrades to safe defaults rather than crashing.

  Scenario: Hot-reload picks up a changed setting
    Given filler removal is currently disabled
    When the user enables filler removal in configuration
    And the transcription "um hello there" is post-processed
    Then the post-processed text is "hello there"

  Scenario: Applies the configured default transform after normalizing
    Given the rephrase client rewrites text to "REWRITTEN"
    And the default transform is configured to "polish"
    When the transcription "um hello" is post-processed
    Then the post-processed text is "REWRITTEN"

  Scenario: An unknown default transform is rejected and the pipeline degrades safely
    Given a post-process configuration whose default transform is the unknown "sparkle"
    When the configuration is applied
    Then a clear validation error about the transform is reported
    When the pipeline is left with that unknown default transform
    And the transcription "hello world" is post-processed
    Then the post-processed text is "hello world"
