# Coverage map (acceptance criterion -> scenario / test):
#  AC1 Home->Audio->Home returns the SAME VM instance; selection/page state survives a round-trip
#         -> "A section's view-model and its state survive a navigation round-trip"
#  AC2 NavigationService no longer disposes the outgoing VM on navigate-away; cached VMs are disposed
#         once when the shell scope is disposed
#         -> Logic.AppManagement.Tests/Shell/NavigationServiceTests (inner loop; WPF-free DI scope)
#  AC3 VMs stay inside the shell UI scope (scoped, not app singletons) and the stale "transient" comment
#         is corrected -> the scoped registration in AppManagementServiceCollectionExtensions + this
#         scenario (a fresh scenario scope yields fresh, isolated instances)

@WHISPER-89
Feature: The shell caches one view-model per section across navigation
  As a user configuring the app through the dashboard
  I want each section to keep its state when I switch tabs and come back
  So that selecting a model (or paging history) is not silently thrown away on navigation

  Scenario: A section's view-model and its state survive a navigation round-trip
    Given the Model section has loaded its list and resolved the active model
    When the user navigates away to the "Audio" section and back to the "Model" section
    Then the Model section is the same view-model instance as before
    And the loaded model list and active selection are still present
