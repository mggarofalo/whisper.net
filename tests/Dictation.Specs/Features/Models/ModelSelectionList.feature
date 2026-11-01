# Coverage map (acceptance criterion -> scenario):
#  AC1 choosing a row makes that model active; the active row carries the selection mark
#         -> "Choosing a model on this device switches to it" + "SettingsDesignSystem: The model list is a selection list"
#  AC2 choosing an un-downloaded model downloads, then switches, saying so on the row
#         -> "Choosing a model that is not downloaded downloads it, then switches"
#  AC3 the mark only moves once activation succeeds; a failure leaves the previous model marked
#         -> "Choosing a model that is not downloaded..." + "A failed switch leaves the previous model in use"
#  AC4 each row shows id, download size and a status in words -> "Each row says what state its model is in"
#  AC5 trailing slot is download lifecycle only               -> "Downloading without switching" + @WHISPER-105 row actions
#  AC6 keyboard (arrows move focus, Space/Enter chooses) is ListBox glue in ModelView.xaml.cs, verified by the
#      view smoke harness and a manual check; recorded as a spec exception in the PR.
#  Also pinned: choosing the model already in use does nothing, and a later choice wins over a pending one.

@WHISPER-149
Feature: The model list is a selection list
  As someone choosing which Whisper model to dictate with
  I want to pick a model by choosing its row in the list
  So that the model in use is the list's selection, not a button I have to decode

  Background:
    Given the model list is loaded with "small.en" in use and "base.en" on this device

  Scenario: Each row says what state its model is in
    Then the "small.en" row reads "In use"
    And the "base.en" row reads "On this device"
    And the "medium" row reads "Not downloaded"
    And every row's caption shows its id, download size and status

  Scenario: Choosing a model on this device switches to it
    When the user chooses "base.en" in the model list
    Then the model list marks "base.en" as in use
    And the "small.en" row reads "On this device"

  Scenario: Choosing a model that is not downloaded downloads it, then switches
    When the user chooses "medium" in the model list while its download is held
    Then the "medium" row reads "Downloading, then switching"
    And the model list still marks "small.en" as in use
    When the held download finishes
    Then the model list marks "medium" as in use

  Scenario: A failed switch leaves the previous model in use
    Given switching to "base.en" will fail
    When the user chooses "base.en" in the model list
    Then the model list still marks "small.en" as in use
    And the "base.en" row explains that it couldn't switch

  Scenario: Choosing the model already in use does nothing
    When the user chooses "small.en" in the model list
    Then no model switch is requested

  Scenario: A later choice wins over a pending download
    When the user chooses "medium" in the model list while its download is held
    And the user chooses "base.en" in the model list
    And the held download finishes
    Then the model list marks "base.en" as in use
    And the "medium" row reads "On this device"

  Scenario: Downloading without switching
    When the user downloads "medium" from its row
    Then the "medium" row reads "On this device"
    And the model list still marks "small.en" as in use
