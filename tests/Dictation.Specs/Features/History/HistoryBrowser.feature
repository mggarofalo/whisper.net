# Coverage map (acceptance criterion -> scenario / test):
#  AC1 display recent history (timestamp + text) from a query -> "Recent transcriptions are listed"
#  AC2 browse/page without blocking the UI thread             -> "Browsing loads the next page"
#  AC3 selecting an entry copies its text via a command        -> "Re-copying a past transcription"
#  AC4 empty history renders an empty state, not an error      -> "Empty history shows an empty state"

@WHISPER-45
Feature: Browsing transcription history
  As someone who dictates
  I want to browse my past transcriptions and re-copy one
  So that I can reuse earlier results

  Scenario: Recent transcriptions are listed
    Given transcriptions have been recorded previously
    When the user opens the history view
    Then the recent transcriptions are listed most-recent-first

  Scenario: Browsing loads the next page
    Given more transcriptions exist than fit on one page
    When the user opens the history view
    And the user browses to the next page
    Then the next page of transcriptions is shown

  Scenario: Re-copying a past transcription
    Given the history view lists a past transcription
    When the user chooses to copy that entry
    Then a copy request is dispatched for that entry's text

  Scenario: Empty history shows an empty state
    Given no transcriptions have been recorded
    When the user opens the history view
    Then the history view shows an empty state
