# Coverage map (acceptance criterion -> scenario / test):
#  AC1 list models with speed/accuracy/RAM ratings from a query  -> "Available models are listed with ratings"
#  AC2 download surfaces live progress + terminal success/failure -> "Downloading shows progress" + "A failed download does not activate the model"
#  AC3 selecting dispatches a switch; active model is reflected   -> "Switching to an already-available model"
#  AC4 selecting an un-downloaded model downloads before activate -> "Downloading shows progress" + "A failed download does not activate the model"

@WHISPER-27
Feature: Model selection
  As someone tuning dictation
  I want to browse, download, and switch Whisper models from the picker
  So that I can trade speed for accuracy on my machine

  Scenario: Available models are listed with ratings
    Given the model catalog is available
    When the model list is loaded
    Then each listed model shows speed, accuracy, and memory ratings

  Scenario: Switching to an already-available model
    Given the model picker lists a downloaded model "small.en"
    When the user selects model "small.en"
    Then a switch-active-model request is dispatched for "small.en"
    And the view shows "small.en" as active

  Scenario: Downloading shows progress
    Given the user selects a model "base.en" that is not yet downloaded
    When the download proceeds
    Then download progress is shown
    And on completion the model becomes active

  Scenario: A failed download does not activate the model
    Given the user selects a model "medium" whose download will fail
    When the download is attempted
    Then the download is marked failed
    And the model is not made active
