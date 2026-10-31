# Coverage map (acceptance criterion -> scenario / test):
#  AC1 no colour literals; Fluent semantic brushes via DynamicResource in one shared dictionary
#         -> "Status and surface colours come from theme-aware semantic brushes"
#  AC2 a shared type scale + spacing tokens; secondary text uses the secondary brush, not Opacity
#         -> "Every settings page uses the shared type scale and spacing"
#  AC3 page titles match nav labels -> "Each page is titled with its navigation label"
#  AC4 Refresh / Load more / status out of the title row; Load more at the bottom of History
#         -> "Page commands stay out of the title row"
#  AC5 the History and Home scrollbars stop covering the row actions
#         -> "Scrolling pages keep the scrollbar clear of their row actions"
#  AC6 the window has a minimum size -> "The settings window cannot shrink below a usable size"
#  AC7 time saved reads "4h 23m" -> TimeSavedFormatterTests (a pure formatting rule; no scenario needed)
#  AC8 list rows have meaningful automation names -> "List rows are announced by their content"
#  AC9 the Home vs Model active-model disagreement -> SettingsWriteIsolation.feature
#  The rendered look in light and dark is verified by before/after screenshots in the PR (manual).

@WHISPER-142
Feature: The settings pages share one theme-aware design system
  As someone who uses Whisper in light or dark mode
  I want every settings page to use the same readable colours, type sizes and layout
  So that the app looks like one product and stays legible in either theme

  Scenario: Status and surface colours come from theme-aware semantic brushes
    Then no settings page declares its own colour literal
    And status and surface colours are Fluent semantic brushes bound live to the theme

  Scenario: Every settings page uses the shared type scale and spacing
    Then every settings page takes its type sizes and spacing from the shared tokens
    And secondary text uses the secondary text brush rather than reduced opacity

  Scenario: Each page is titled with its navigation label
    Then every settings page title matches its navigation label

  Scenario: Page commands stay out of the title row
    Then no page title shares a toolbar row with its commands
    And the History page offers "Load more" below its list

  Scenario: Scrolling pages keep the scrollbar clear of their row actions
    Then the scrolling settings pages reserve a gutter for the scrollbar

  Scenario: The settings window cannot shrink below a usable size
    Then the settings window declares a minimum width and height

  Scenario: List rows are announced by their content
    Then the History, Home and Model list rows expose their content as the automation name
