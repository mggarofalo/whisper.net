# Coverage map (acceptance criterion -> scenario):
#  AC1 one shared "In use" card: name, id, size, meters; a model not downloaded yet says so
#         -> SettingsDesignSystem "Both pages show the same in-use card" + the two scenarios below
#  AC2 the Model page's card follows the committed selection
#         -> SettingsDesignSystem "Both pages show the same in-use card" (it binds ActiveModel) +
#            ModelSelectionList.feature (ActiveModel moves only once a switch succeeds)
#  AC3 Home shows the card from the model list, matching the model settings say dictation loads
#         -> "Home summarizes the model dictation will load" + "Home says when the model in use isn't downloaded yet"
#  AC4 no Model page subtitle -> SettingsDesignSystem "The Model page has no subtitle"

@WHISPER-153
Feature: Home shows the model in use
  As someone checking how dictation is set up
  I want Home to summarize the model in use the same way the Model page does
  So that I can see its size and trade-offs without opening the Model page

  Scenario: Home summarizes the model dictation will load
    Given the dashboard's settings select model "small.en" and the system default input device
    And the model "small.en" is on this device
    When the Home section is opened
    Then Home's in-use card shows "small.en" with its name, size and ratings
    And the dashboard shows "small.en" as the active model

  Scenario: Home says when the model in use isn't downloaded yet
    Given the dashboard's settings select model "medium.en" and the system default input device
    When the Home section is opened
    Then Home's in-use card shows "medium.en" as not downloaded yet