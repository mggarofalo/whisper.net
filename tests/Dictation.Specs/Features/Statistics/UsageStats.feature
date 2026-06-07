@WHISPER-48
Feature: Usage statistics are aggregated from history
  As someone using the dictation app
  I want my dictation totals summarized from history
  So that the dashboard can show how much I have dictated

  # AC: stats are aggregated from recorded transcriptions.
  Scenario: Stats are computed from recorded transcriptions
    Given the history store contains transcriptions totaling 150 words across 3 sessions
    When usage statistics are requested
    Then the returned usage stats report 150 words and 3 sessions

  # AC: empty history yields zeroed stats and never throws.
  Scenario: Empty history yields zeroed stats
    Given the history store is empty
    When usage statistics are requested
    Then the returned usage stats report 0 words and 0 sessions
