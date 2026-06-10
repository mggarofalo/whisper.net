# Coverage map (acceptance criterion -> scenario / test):
#  AC1 A failed transcription/delivery raises a tray balloon with a clear message; the app stays alive
#      (spec injects a failing port, asserts a notification requested)
#         -> "A failed transcription surfaces a notification and the pipeline returns to idle"
#         -> "A capture-device failure surfaces a notification and the pipeline returns to idle"
#  AC2 The notifier marshals to the UI thread via IUiDispatcher and degrades gracefully if
#      notifications are suppressed (logs, never throws)
#         -> "A background-thread notification is marshaled through the dispatcher seam"
#         -> "A suppressed notification is logged and never throws"
#         -> "A failing balloon presenter never breaks the caller"
#  AC3 DispatcherUnhandledException additionally surfaces a non-technical notice instead of failing
#      silently
#         -> "An unhandled UI exception surfaces a non-technical notice" (artifact: the composition
#            root's dispatcher-exception handler notifies the user; the message contains no exception
#            details — the technical record stays in the log)

@WHISPER-95
Feature: Backend failures surface to the user instead of dying in the log
  As a user of a windowless tray app
  I want a failed dictation to tell me something went wrong
  So that I am not left wondering why nothing was typed

  Scenario: A failed transcription surfaces a notification and the pipeline returns to idle
    Given a dictation pipeline whose transcription fails
    When an utterance is recorded and stopped
    Then a user notification reports the dictation failure
    And the pipeline has returned to idle

  Scenario: A capture-device failure surfaces a notification and the pipeline returns to idle
    Given a dictation pipeline whose capture device fails mid-recording
    When recording starts and the device failure strikes
    Then a user notification reports the dictation failure
    And the pipeline has returned to idle

  Scenario: A background-thread notification is marshaled through the dispatcher seam
    Given a tray notifier bound to a test UI dispatcher and a recording balloon
    When an error notification is raised off the UI thread
    Then the balloon request was marshaled through the dispatcher seam

  Scenario: A suppressed notification is logged and never throws
    Given a tray notifier with no balloon presenter attached
    When an error notification is raised off the UI thread
    Then the notification is swallowed without an exception

  Scenario: A failing balloon presenter never breaks the caller
    Given a tray notifier whose balloon presenter throws
    When an error notification is raised off the UI thread
    Then the notification is swallowed without an exception

  Scenario: An unhandled UI exception surfaces a non-technical notice
    Then the dispatcher exception handler notifies the user with a non-technical message
