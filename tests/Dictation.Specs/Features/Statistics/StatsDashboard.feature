# Coverage map (acceptance criterion -> scenario / test):
#  AC1 display key usage stats (transcriptions, words, time saved) from a query -> "Stats reflect recorded usage"
#  AC2 stats refresh to reflect new activity                                    -> "Refreshing reflects new activity"
#  AC3 with no activity, show zeroed stats rather than an error                 -> "No usage yet shows zeroes"
#  AC4 aggregation lives behind the Application layer (not the ViewModel)       -> "Stats reflect recorded usage" (totals are the calculator's output)

@WHISPER-53
Feature: Stats dashboard
  As someone who dictates
  I want to see how much I've transcribed and the time it saved
  So that I can see the value of the tool at a glance

  Scenario: Stats reflect recorded usage
    Given usage metrics have been recorded
    When the user opens the stats dashboard
    Then the displayed totals match the recorded usage

  Scenario: Refreshing reflects new activity
    Given usage metrics have been recorded
    When the user opens the stats dashboard
    And more activity is recorded and the dashboard is refreshed
    Then the displayed totals include the new activity

  Scenario: No usage yet shows zeroes
    Given no usage metrics have been recorded
    When the user opens the stats dashboard
    Then the stats display zeroed values without error
