# Coverage map (acceptance criterion -> scenario):
#  AC9 Home and Model must agree on the active model. Root cause, reproduced here: each settings section
#      committed its single-field change by writing back the whole settings snapshot it loaded earlier, so
#      changing the theme (or display, device, hotkey) after switching models reverted the persisted model.
#      Home then showed the reverted model from settings while Model showed the one actually loaded.
#         -> "A section's own change never reverts a model switched elsewhere" (one example per section)

@WHISPER-142
Feature: Changing one setting never reverts another
  As someone who switches models and then adjusts other settings
  I want each change to touch only the setting I changed
  So that the app never silently undoes my earlier choices or disagrees with itself

  Scenario Outline: A section's own change never reverts a model switched elsewhere
    Given the persisted active model is "base.en"
    And the "<section>" settings section is open
    When the active model is switched to "small.en" elsewhere in the app
    And the user changes the "<section>" section's setting
    Then the "<section>" section's change is persisted
    And the persisted active model is still "small.en"
    And Home and Model agree that "small.en" is the active model

    Examples:
      | section |
      | Theme   |
      | General |
      | Audio   |
      | Hotkey  |
