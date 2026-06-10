# Coverage map (acceptance criterion -> scenario / test):
#  AC1 An ADR/section documents: view resolved by implicit DataTemplate keyed on VM type; VM supplied
#      by the container; no ViewModelLocator / per-view code-behind DataContext
#         -> "The view-resolution convention is documented"
#         -> "Every shell section's view-model resolves a view through an implicit data template"
#  AC2 LevelOverlay no longer switches on e.PropertyName; visibility + meter are data-bound (a renamed
#      property is a binding error, not a silent no-op)
#         -> "No view reacts to view-model changes by property-name string matching" (the overlay and
#            the tray icon now bind; renames refactor the nameof-based binding paths or break loudly)
#  AC3 A grep/check confirms no feature-view code-behind has logic beyond InitializeComponent
#         -> "Feature-view code-behind carries no logic beyond construction"
#         -> "Selecting a capture device commits it without view logic" + "A reload's programmatic
#            selection is not committed" (the commit decision that lived in AudioDeviceView code-behind
#            now lives in the view-model, where the specs drive it for real)

@WHISPER-92
Feature: Views resolve from view-model types and carry no code-behind logic
  As a developer evolving the WPF shell
  I want views resolved by implicit data templates over container-supplied view-models
  So that renaming or rebinding fails loudly and no behavior hides in untestable code-behind

  Scenario: The view-resolution convention is documented
    Then the architecture guide records the implicit-DataTemplate view-resolution convention

  Scenario: Every shell section's view-model resolves a view through an implicit data template
    Then each registered navigation section has a data template mapping its view-model to a view

  Scenario: Feature-view code-behind carries no logic beyond construction
    Then no feature view code-behind contains logic beyond its constructor

  Scenario: No view reacts to view-model changes by property-name string matching
    Then no view switches on property-change names

  Scenario: Selecting a capture device commits it without view logic
    Given the device picker has loaded two available devices
    When the selected device changes to "mic-b"
    Then the device choice "mic-b" is persisted exactly once

  Scenario: A reload's programmatic selection is not committed
    Given a persisted capture device that is no longer connected
    When the device picker loads and falls back to the system default
    Then no device choice is persisted
