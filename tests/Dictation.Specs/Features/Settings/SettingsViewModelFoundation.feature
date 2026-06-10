# Coverage map (acceptance criterion -> scenario / test):
#  AC1 settings/feature VMs derive from ObservableValidator; no magic-string raises
#         -> "Every feature section view-model is a validation-capable observable"
#  AC2 all bindable state uses [ObservableProperty]; all commands use [RelayCommand]
#         -> "A feature view-model raises change notification for its bindable state" (source-generated
#            change notification proves the property is an [ObservableProperty]) +
#            Logic.AppManagement.Tests/Shell/SettingsViewModelObservabilityTests (every generated property)
#  AC3 existing tests still pass; new tests assert INotifyPropertyChanged fires per generated property
#         -> Logic.AppManagement.Tests/Shell/SettingsViewModelObservabilityTests

@WHISPER-76
Feature: Settings and feature view-models share a validation-capable observable base
  As the foundation for native, validated settings UI
  I want every settings/feature view-model to be an ObservableValidator with source-generated change notification
  So that validation (WHISPER-77) and instant-apply (WHISPER-78) build on one uniform observable base

  Scenario: Every feature section view-model is a validation-capable observable
    Given the shell's feature section view-models
    Then each one is a validation-capable observable view-model

  Scenario: A feature view-model raises change notification for its bindable state
    Given the Hotkey section view-model
    When its current hotkey is set to "Ctrl+Alt+D"
    Then it raises a property-changed notification for the current hotkey
