# Coverage map (acceptance criterion -> scenario / test):
#  AC1 A helper registers a collection + lock on the UI thread before binding; mutations take the lock
#         -> "The history list is registered for cross-thread binding at construction" (registration
#            happens in the view-model constructor, before any view can bind)
#         -> "Collection mutations take the registered lock" (a mutation blocks while another thread
#            holds the gate) + unit depth in UiBoundCollectionTests (every mutation kind holds the gate)
#         -> the WPF half (WpfCollectionSynchronizer) registers through the IUiDispatcher CheckAccess
#            fast-path, so production registration runs on the UI thread
#  AC2 A test mutates a registered collection from a non-UI thread (Task.Run) with no cross-thread
#      exception
#         -> "Loading history from a background thread populates the list safely" (the live WPF
#            CollectionView proof over a real binding rides the WHISPER-96 STA smoke harness)
#  AC3 The convention is documented so new list-bearing VMs adopt it by default
#         -> "The collection-synchronization convention is documented"

@WHISPER-91
Feature: Bound collections tolerate background-thread mutation
  As a developer adding live list features
  I want bound collections registered with a shared lock and mutated under it
  So that an off-UI-thread update never throws a cross-thread exception once the view binds

  Scenario: The history list is registered for cross-thread binding at construction
    Given a history view-model built over the collection-sync seam
    Then its entries collection is registered together with its lock

  Scenario: Collection mutations take the registered lock
    Given a synchronized bindable collection
    And another thread is holding the collection's lock
    When an item is added on a background thread
    Then the add completes only after the lock is released

  Scenario: Loading history from a background thread populates the list safely
    Given a history view-model with persisted entries
    When the history loads on a background thread
    Then the entries are listed with no cross-thread failure

  Scenario: The collection-synchronization convention is documented
    Then the architecture guide records the collection-synchronization convention
