# Coverage map (acceptance criterion -> scenario / test):
#  AC1 display recent history (timestamp + text) from a query -> "Recent transcriptions are listed"
#  AC2 browse/page without blocking the UI thread             -> "Browsing loads the next page"
#  AC3 selecting an entry copies its text via a command        -> "Re-copying a past transcription"
#  AC4 empty history renders an empty state, not an error      -> "Empty history shows an empty state"
#  WHISPER-110 AC3 the next-page control is disabled when no further page exists -> the @WHISPER-110 scenarios
#  WHISPER-146 real pagination (previous/next + page indicator) and store-side text search:
#    AC1 page indicator, previous/next                         -> "The page indicator shows ...", "Going back ..."
#    AC2 previous disabled on page 1, next disabled on the last -> "Going back ...", the @WHISPER-110 scenarios
#    AC3/AC4 case-insensitive search, filtered in the store    -> "Searching filters ... case-insensitively"
#    AC5 paging over filtered results; a new search -> page 1  -> "Search results are paged", "Changing the search ..."
#    AC6 a distinct empty state when nothing matches           -> "A search with no matches ..."

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

  # WHISPER-110: browsing past the end of the history silently did nothing. The browser knows the
  # total number of entries, so the next-page control is disabled whenever no further page exists.
  @WHISPER-110
  Scenario: The next page is unavailable when all entries fit on one page
    Given transcriptions have been recorded previously
    When the user opens the history view
    Then no further history pages are offered

  @WHISPER-110
  Scenario: The next page is unavailable when exactly one full page exists
    Given exactly one full page of transcriptions exists
    When the user opens the history view
    Then no further history pages are offered

  @WHISPER-110
  Scenario: The next page is available while a further page exists
    Given more transcriptions exist than fit on one page
    When the user opens the history view
    Then a further history page is offered

  @WHISPER-110
  Scenario: The next page becomes unavailable on the last page
    Given more transcriptions exist than fit on one page
    When the user opens the history view
    And the user browses to the next page
    Then no further history pages are offered

  @WHISPER-146
  Scenario: The page indicator shows the current page and the page count
    Given more transcriptions exist than fit on one page
    When the user opens the history view
    Then the history view is on page 1 of 2

  @WHISPER-146
  Scenario: Going back returns to the newer page
    Given more transcriptions exist than fit on one page
    When the user opens the history view
    And the user browses to the next page
    And the user browses to the previous page
    Then the history view is on page 1 of 2
    And the newest transcription is listed first
    And no previous history page is offered

  @WHISPER-146
  Scenario: Searching filters transcriptions case-insensitively
    Given these transcriptions have been recorded, oldest first:
      | text             |
      | Buy MILK         |
      | call mom         |
      | milkshake recipe |
      | walk the dog     |
    When the user opens the history view
    And the user searches history for "milk"
    Then only these transcriptions are listed:
      | text             |
      | milkshake recipe |
      | Buy MILK         |
    And the search was answered by the history store

  @WHISPER-146
  Scenario: Search results are paged
    Given 30 transcriptions mention "meeting" among others that do not
    When the user opens the history view
    And the user searches history for "MEETING"
    Then the history view is on page 1 of 2
    When the user browses to the next page
    Then 5 transcriptions are listed
    And no further history pages are offered

  @WHISPER-146
  Scenario: Changing the search returns to the first page
    Given 30 transcriptions mention "meeting" among others that do not
    When the user opens the history view
    And the user browses to the next page
    And the user searches history for "meeting"
    Then the history view is on page 1 of 2

  @WHISPER-146
  Scenario: Clearing the search lists the whole history again
    Given transcriptions have been recorded previously
    When the user opens the history view
    And the user searches history for "newest"
    And the user clears the history search
    Then the recent transcriptions are listed most-recent-first

  @WHISPER-146
  Scenario: A search with no matches shows a no-matches state
    Given transcriptions have been recorded previously
    When the user opens the history view
    And the user searches history for "zebra"
    Then the history view shows that nothing matches the search
    And the history view does not show the empty-history state
