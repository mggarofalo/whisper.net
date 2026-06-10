# Coverage map (acceptance criterion -> scenario / test):
#  AC1 No production view-model references Application.Current.Dispatcher (grep-enforceable); all
#      marshaling goes through IUiDispatcher with a CheckAccess fast-path
#         -> "No production code touches the WPF application dispatcher directly" (grep-enforced)
#         -> "A status change already on the UI thread skips the dispatcher round-trip" (fast-path)
#  AC2 High-frequency LevelChanged updates use the non-blocking async path (the audio thread never
#      blocks per frame)
#         -> "High-frequency level updates never block the audio thread" — the seam has no blocking
#            call at all (Post / InvokeAsync only), and the scenario asserts the posted path was used
#  AC3 A unit test drives the VM handlers with a synchronous test IUiDispatcher and no live
#      Application (these become unit-testable); null-safe at shutdown
#         -> every scenario here runs the real view-models over a synchronous test dispatcher with no
#            live WPF Application; unit depth in Logic.AppManagement.Tests (TrayIconViewModelTests,
#            LevelOverlayViewModelTests); the WPF implementation (WpfUiDispatcher) wraps the dispatcher
#            captured at startup — never Application.Current — and no-ops once dispatcher shutdown
#            has begun, so nothing dereferences a torn-down Application.

@WHISPER-90
Feature: View-models marshal updates through an injectable UI dispatcher seam
  As the windowless tray app
  I want view-models to marshal controller events through an injected UI dispatcher
  So that background-thread updates are safe in production and the view-models stay testable without a live WPF application

  Scenario: A background status change reaches the tray view-model through the dispatcher seam
    Given a tray icon view-model bound to a test UI dispatcher
    When the recording state changes off the UI thread
    Then the status update is marshaled through the dispatcher seam
    And the tray view-model reflects the recording status and tooltip

  Scenario: A status change already on the UI thread skips the dispatcher round-trip
    Given a tray icon view-model bound to a test UI dispatcher
    And the caller is already on the UI thread
    When the recording state changes
    Then the update is applied without a dispatcher round-trip
    And the tray view-model reflects the recording status and tooltip

  Scenario: High-frequency level updates never block the audio thread
    Given a level overlay view-model bound to a test UI dispatcher
    When recording starts and an audio frame arrives off the UI thread
    Then the level update is posted without blocking the calling thread
    And the overlay view-model reflects the new level

  Scenario: No production code touches the WPF application dispatcher directly
    Then no production source file references the WPF application dispatcher
