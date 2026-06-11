# Coverage map (acceptance criterion -> scenario / test):
#  AC1 Model, Audio, History, and Stats are populated on first open with no manual Refresh
#         -> "The Model section is populated when first opened"
#         -> "The Audio section lists capture devices when first opened"
#         -> "The History section shows past transcriptions when first opened"
#         -> "The Stats section shows recorded totals when first opened"
#  AC2 Rapid tab switching does not spam duplicate queries
#         -> "Returning to an already-loaded section does not re-query"
#         -> "An in-flight first load cannot be double-fired"
#         -> (and Refresh stays the explicit manual re-query) "A manual refresh re-queries the store"
#  AC3 Reqnroll scenarios cover activation-triggered load
#         -> this feature: every scenario enters through the REAL navigation lifecycle
#            (ShellViewModel.NavigateCommand), never LoadCommand directly
#  Unit depth: Logic.AppManagement.Tests/Shell/FeatureViewModelFirstActivationTests (the base hook
#  fires once per cached instance, and re-activation never re-fires it)

@WHISPER-108
Feature: Sections load their data on first activation
  As a user opening the dashboard
  I want every section populated as soon as I first open it
  So that I never face an empty pane that demands a manual Refresh

  Scenario: The Model section is populated when first opened
    When the user opens the "Model" section
    Then the model list is populated without a manual refresh

  Scenario: The Audio section lists capture devices when first opened
    Given capture devices "Headset Mic" and "USB Mic" are available
    When the user opens the "Audio" section
    Then the device picker lists "Headset Mic" and "USB Mic" without a manual refresh

  Scenario: The History section shows past transcriptions when first opened
    Given the history store holds a transcription "hello world"
    When the user opens the "History" section
    Then the history list shows "hello world" without a manual refresh

  Scenario: The Stats section shows recorded totals when first opened
    Given the history store holds two recorded transcriptions
    When the user opens the "Stats" section
    Then the dashboard shows the recorded totals without a manual refresh

  Scenario: Returning to an already-loaded section does not re-query
    Given the history store holds a transcription "hello world"
    And the user has opened the "History" section
    When the user rapidly switches away and back to the "History" section twice
    Then the history was queried exactly once

  Scenario: A manual refresh re-queries the store
    Given the history store holds a transcription "hello world"
    And the user has opened the "History" section
    When the user refreshes the history section
    Then the history was queried exactly twice

  Scenario: An in-flight first load cannot be double-fired
    Given the history load will not complete until released
    When the user opens the "History" section while the load is pending
    And a duplicate load is attempted the way the view invokes it
    And the pending history load completes
    Then the duplicate attempt was refused
    And the history was queried exactly once
