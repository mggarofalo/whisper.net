# Coverage map (acceptance criterion -> scenario):
#  AC2 only contextually valid actions are shown per row (Download / Cancel / Select)
#         -> "Each row offers only the action that fits its state" + "A downloading row offers only Cancel"
#  AC1 (the full list is usable at the default window size without horizontal scrolling) and AC3 (the
#      selected model is visually indicated) are layout/visual outcomes validated by the WHISPER-96 smoke
#      harness + a manual check; recorded as a spec exception in the PR.

@WHISPER-105
Feature: The model list shows only the action that fits each row's state
  As someone managing Whisper models
  I want each row to show only Download, Cancel, or Select as appropriate
  So that the list is compact instead of a permanent three-button strip

  Scenario: Each row offers only the action that fits its state
    Given the model rows are loaded with "small.en" active and "base.en" downloaded
    Then the "medium" row offers only its Download action
    And the "base.en" row offers only its Select action
    And the "small.en" row offers no row action and is shown as the selected model

  Scenario: A row that is downloading offers only Cancel
    Given the model rows are loaded with nothing downloaded
    When a download is begun on "medium"
    Then the "medium" row offers only its Cancel action
