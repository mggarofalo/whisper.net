# Coverage map (acceptance criterion -> scenario / test):
#  AC1 Feature VMs register subscriptions in OnActivated and remove them in OnDeactivated; a test
#      asserts an inactive cached VM gets no callbacks
#         -> "An active section receives a live settings change"
#         -> "An inactive cached section receives no callbacks"
#         -> "Returning to a section resubscribes it"
#         -> unit depth in FeatureViewModelLifecycleTests (registered exactly while active)
#  AC2 Controller/messenger subscriptions are leak-free (activate/deactivate/collect test, or
#      WeakReferenceMessenger)
#         -> production standardizes on WeakReferenceMessenger (registered by AddApplication);
#            FeatureViewModelLifecycleTests proves a registered, never-deactivated view-model is still
#            collectable — the messenger cannot root it
#  AC3 Documented rule: cached VMs are deactivated on navigate-away, disposed only at shell teardown
#         -> "The activation lifecycle rule is documented"

@WHISPER-94
Feature: Cached view-models subscribe while active and let go when deactivated
  As a user switching between settings sections
  I want each cached section live only while I am looking at it
  So that background sections neither react to stale events nor leak subscriptions

  Scenario: An active section receives a live settings change
    Given the shell is open on the "Hotkey" section
    When a settings change with hotkey "Ctrl+Alt+Z" is published
    Then the hotkey section shows "Ctrl+Alt+Z" as the current hotkey

  Scenario: An inactive cached section receives no callbacks
    Given the shell is open on the "Hotkey" section
    And the user navigates away to the "History" section
    When a settings change with hotkey "Ctrl+Alt+Z" is published
    Then the cached hotkey section does not react to the change

  Scenario: Returning to a section resubscribes it
    Given the shell is open on the "Hotkey" section
    And the user navigates away to the "History" section
    And the user navigates back to the "Hotkey" section
    When a settings change with hotkey "Ctrl+Alt+Z" is published
    Then the hotkey section shows "Ctrl+Alt+Z" as the current hotkey

  Scenario: The activation lifecycle rule is documented
    Then the architecture guide records the activation lifecycle rule
