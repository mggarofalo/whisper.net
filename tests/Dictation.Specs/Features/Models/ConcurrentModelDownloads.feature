# Coverage map (acceptance criterion -> scenario):
#  AC1 two or more models download simultaneously, each with its own progress bar and Cancel
#         -> "Two models download at the same time, each with its own progress and Cancel"
#  AC2 other rows remain fully interactive while a download runs
#         -> "A row stays downloadable while another row is already downloading"
#  AC3 cancelling one download does not affect the others
#         -> "Cancelling one download leaves the other running"

@WHISPER-107
Feature: Concurrent model downloads with independent per-row state
  As someone setting up several Whisper models
  I want to download more than one at once, each with its own progress and Cancel
  So that starting one download never blocks, disables, or cancels the others

  Background:
    Given the model picker is loaded for concurrent downloads

  Scenario: Two models download at the same time, each with its own progress and Cancel
    When the user begins downloading "base.en"
    And the user begins downloading "small.en"
    Then the "base.en" download is running with its own progress
    And the "small.en" download is running with its own progress
    And the "base.en" download can be cancelled on its own row
    And the "small.en" download can be cancelled on its own row

  Scenario: A row stays downloadable while another row is already downloading
    When the user begins downloading "base.en"
    Then the "base.en" download is running with its own progress
    And the "small.en" row can still start its own download

  Scenario: Cancelling one download leaves the other running
    When the user begins downloading "base.en"
    And the user begins downloading "small.en"
    And the user cancels the "base.en" download
    Then the "base.en" download has reset
    And the "small.en" download is running with its own progress
