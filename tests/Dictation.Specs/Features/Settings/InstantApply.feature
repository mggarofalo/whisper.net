# Coverage map (acceptance criterion -> scenario / test):
#  AC1 committing a change reconfigures the live service within one message round-trip (no restart)
#         -> "Committing a hotkey change reconfigures the live matcher immediately" (the UpdateSettingsCommand
#            handler publishes on the channel; the weakly-registered hotkey service rebinds the live controller)
#  AC2 recipients use weak registration; a test proves a change triggers reconfiguration
#         -> same scenario (HotkeyConfigurationHostedService registers via WeakReferenceMessenger, no manual
#            unsubscribe) + Logic.AppManagement.Tests/Lifecycle/SettingsLifecycleServiceTests
#  AC3 invalid values never reach the service (validation-gated); free-text settings are debounced
#         -> Application.Tests/Settings/SettingsChangeChannelTests (debounce) + the ValidationBehavior pipeline
#            already short-circuits invalid UpdateSettingsCommand before the handler publishes

@WHISPER-78
Feature: Committed settings changes apply to live services without a restart
  As a user adjusting settings while the app runs
  I want a committed change to reconfigure the running service immediately
  So that I never have to restart the app for a setting to take effect

  Scenario: Committing a hotkey change reconfigures the live matcher immediately
    Given the dictation pipeline has started
    When the dictation hotkey is changed to "Ctrl+Alt+Space"
    Then the activation controller matches the chord "Ctrl+Alt+Space"
