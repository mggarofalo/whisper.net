# Coverage map (acceptance criterion -> scenario / evidence):
#  AC1 Serilog writes to a rolling file under a per-user logs directory (in addition to console)
#         -> "Application events are persisted to a log file"
#  AC2 log location documented -> README / docs/packaging.md (see PR)
#  AC3 unhandled dispatcher exception is recorded; recoverable UI errors do not kill the process
#         -> WPF App glue (smoke-only per the Presentation/specs split); the file sink this feature adds
#            is what makes that record land on disk
#  AC4 first-run onboarding detection no longer blocks the UI thread
#         -> WPF App glue (smoke-only); covered by the async OnStartup change in the PR

@WHISPER-73
Feature: Diagnosable logging for the installed app
  As a maintainer triaging a bug report from an installed tray app
  I want application events written to a persistent log file
  So that failures are diagnosable instead of vanishing into an invisible console

  Scenario: Application events are persisted to a log file
    Given logging is configured for a per-user application-data logs directory
    When the application logs an informational event
    Then the event is written to a rolling log file in that directory
