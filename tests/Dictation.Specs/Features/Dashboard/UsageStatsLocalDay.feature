# Coverage map (acceptance criterion -> scenario / test):
#  AC1/AC2 daily buckets reflect the user's local day; a non-UTC zone is pinned deterministically
#         -> "Two dictations straddling local midnight fall on different days"
#  AC3 storage/query timestamps stay UTC -> structural: only the bucketing converts; entries are UTC
#  AC4 all-time totals unaffected -> the all-time total assertion here + the existing @WHISPER-24 specs
#  Unit depth: Logic.AppManagement.Tests/UsageSummaryCalculatorTests (local-day grouping)

@WHISPER-116
Feature: Usage stats group by the user's local day
  As someone reviewing my dictation usage
  I want each day's totals to follow my local calendar day, not the UTC day
  So that an evening dictation counts toward today, not tomorrow

  Scenario: Two dictations straddling local midnight fall on different days
    Given the user's time zone is 5 hours behind UTC
    And a dictation recorded at 2026-06-12 04:30 UTC
    And a dictation recorded at 2026-06-12 05:30 UTC
    When the usage summary is calculated
    Then the summary has 2 daily buckets
    And the all-time transcription total is 2
