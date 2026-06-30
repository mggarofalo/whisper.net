# Coverage map (acceptance criterion -> scenario / test):
#  AC1 dictating while History is open (or switching to it) shows the new entry without Refresh
#         -> "Dictating while History is open shows the new entry without Refresh" +
#            "A live entry survives switching away and back"
#  AC2 newest-first at the top; already-loaded pages and scroll preserved
#         -> "A new entry is prepended above the already-loaded entries"
#  AC3 IsEmpty clears on the first live entry; HasMorePages not corrupted
#         -> "...no longer empty" + Logic.AppManagement.Tests/Shell/HistoryViewModelLiveFeedTests
#  AC4 UI-thread-safe through the synchronizer; @WHISPER-114 scenario via real VM + Mediator + messenger
#         -> these scenarios drive the real HistoryViewModel over the real Mediator pipeline and messenger

@WHISPER-114
Feature: The history list updates live when a transcription is recorded
  As someone dictating with the History tab open
  I want a new transcription to appear in the list immediately
  So that I do not have to click Refresh to see what I just dictated

  Scenario: Dictating while History is open shows the new entry without Refresh
    Given the History section is open with no history yet
    When a transcription "hello world" is recorded
    Then the new transcription appears at the top of the history list
    And the history list is no longer empty

  Scenario: A new entry is prepended above the already-loaded entries
    Given the History section is open showing "older note" and "another note"
    When a transcription "fresh note" is recorded
    Then the new transcription appears at the top of the history list
    And the history list has 3 entries
    And the history list still shows "older note"

  Scenario: A live entry survives switching away and back
    Given the History section is open with no history yet
    When a transcription "kept entry" is recorded
    And the user switches away from History and back
    Then the new transcription appears at the top of the history list
    And the history list has 1 entry

  # WHISPER-136: the live feed is persistent, so an entry recorded while History is NOT the visible tab
  # still appears when the user returns — without a re-query that would disturb the browsed page.
  @WHISPER-136
  Scenario: A transcription recorded while History is inactive still appears on return
    Given the History section is open with no history yet
    And the user navigates away from History
    When a transcription "away note" is recorded
    And the user returns to History
    Then the new transcription appears at the top of the history list
    And the history list has 1 entry
