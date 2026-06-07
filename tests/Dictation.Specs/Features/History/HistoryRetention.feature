@WHISPER-17
Feature: History retention and paged browsing
  Transcription history is kept within a configurable limit — the oldest entries are pruned once the limit
  is exceeded — and can be browsed a page at a time, most-recent-first. Invalid paging requests are
  rejected before they reach the store. Exercised end-to-end through Mediator over a real temp SQLite DB.

  # AC: a configurable retention limit prunes the oldest entries past the limit after a new write.
  Scenario: Oldest entries are pruned past the limit
    Given a retention limit of 100 entries
    And the history already contains 100 entries
    When a new transcription is recorded
    Then the history contains 100 entries
    And the oldest prior entry has been removed

  # AC: GetHistory supports paging and most-recent-first ordering.
  Scenario Outline: Browse history by page
    Given the history already contains 25 entries
    When I browse history with page size <size> and page <page>
    Then I receive <count> history entries
    And they are ordered most-recent-first

    Examples:
      | size | page | count |
      | 10   | 1    | 10    |
      | 10   | 3    | 5     |
      | 50   | 1    | 25    |

  # AC: invalid query inputs are rejected by the validation pipeline before the handler.
  Scenario: A negative page size is rejected
    When I browse history with page size -1 and page 1
    Then the browse request is rejected

  Scenario: A page below one is rejected
    When I browse history with page size 10 and page 0
    Then the browse request is rejected
