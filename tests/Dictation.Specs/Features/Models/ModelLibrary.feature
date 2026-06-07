@WHISPER-4
Feature: Model registry, cache, and download
  As someone choosing a transcription model
  I want known models listed, detected in a local cache, and downloaded on request
  So that I can get a model once and reuse it, without anything fetched behind my back

  Scenario: Detect a cached model without network
    Given the "base" model is already present in the cache
    When the model's cache status is queried
    Then the model is reported as available
    And no network request is made

  Scenario Outline: Download a missing model with progress
    Given the "<model>" model is not present in the cache
    When the user requests its download
    Then progress is reported until completion
    And the verified model file is present in the cache afterward

    Examples:
      | model |
      | tiny  |
      | base  |
