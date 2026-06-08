# Coverage map (acceptance criterion -> scenario / evidence):
#  AC1 auto-update wired (check / download / apply) against a configured channel
#         -> "A newer release is downloaded and applied" (policy over IUpdateSource; Velopack adapter in Infra)
#  AC2 installer + exe Authenticode-signed; cert from a secret, never committed
#         -> "The installer is code-signed when a signing certificate is provided" (pack.ps1 plumbing);
#            a real signed build with a provisioned cert is environmental -> follow-up
#  AC3 update check disclosed as outbound network; feed is the only egress, documented
#         -> README network-disclosure block + CHANGELOG (opt-in, off by default)
#  AC4 failed/unavailable updates degrade gracefully (keep running, logged, no crash)
#         -> "The update channel is unreachable"
#  AC5 a released version updates an older install end-to-end on a test machine
#         -> environmental (needs an installed app + a published release) -> follow-up
#  Privacy: no network egress without opt-in -> "Automatic updates are opt-in and off by default"

@WHISPER-29
Feature: Signed auto-update
  As a user
  I want the app to update itself from a trusted release channel when I opt in
  So that I get fixes without manual downloads — and a failed update never breaks the app

  Scenario: A newer release is downloaded and applied
    Given automatic updates are enabled
    And a newer release "0.2.0" is available on the channel
    When the app checks for updates
    Then the update "0.2.0" is downloaded and staged to apply

  Scenario: The update channel is unreachable
    Given automatic updates are enabled
    And the update channel is unreachable
    When the app checks for updates
    Then the app continues on the current version
    And the failure is logged

  Scenario: No update is available
    Given automatic updates are enabled
    And the app is already up to date
    When the app checks for updates
    Then no update is applied

  Scenario: Automatic updates are opt-in and off by default
    Given automatic updates are disabled
    When the app checks for updates
    Then no update check is performed

  Scenario: The installer is code-signed when a signing certificate is provided
    Given the packaging configuration
    Then the installer is code-signed when a signing certificate is supplied from a secret
