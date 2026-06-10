# Coverage map (acceptance criterion -> scenario / test):
#  AC1 display recent history (timestamp + text) from a query -> "Recent transcriptions are listed"
#  AC2 browse/page without blocking the UI thread             -> "Browsing loads the next page"
#  AC3 selecting an entry copies its text via a command        -> "Re-copying a past transcription"
#  AC4 empty history renders an empty state, not an error      -> "Empty history shows an empty state"
#  WHISPER-110 AC3 Load More disabled when no further pages exist -> the four @WHISPER-110 scenarios

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

  # WHISPER-110: browsing past the end of the history silently did nothing, so the view offered
  # "Load more" forever. The browser now tracks whether a further page may exist, and the view
  # disables Load More when it cannot produce one.
  @WHISPER-110
  Scenario: Load more is unavailable when all entries fit on one page
    Given transcriptions have been recorded previously
    When the user opens the history view
    Then no further history pages are offered

  @WHISPER-110
  Scenario: Load more stays available while a further page may exist
    Given exactly one full page of transcriptions exists
    When the user opens the history view
    Then a further history page is offered

  @WHISPER-110
  Scenario: Load more becomes unavailable once the history is exhausted
    Given exactly one full page of transcriptions exists
    When the user opens the history view
    And the user browses to the next page
    Then no further history pages are offered

  @WHISPER-110
  Scenario: Load more becomes unavailable when the final page is short
    Given more transcriptions exist than fit on one page
    When the user opens the history view
    And the user browses to the next page
    Then no further history pages are offered
