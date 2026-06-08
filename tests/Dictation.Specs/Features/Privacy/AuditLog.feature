@WHISPER-34
Feature: Privacy-gated audit logging
  Transcript text is kept in history as normal, but the verbose audit log is privacy-sensitive: it is
  disabled by default and writes only after the user explicitly opts in. Enabling or disabling it takes
  effect immediately, the data stays on the device, and a user-initiated purge clears both history and the
  audit log. Exercised end-to-end over a real temp SQLite DB.

  # AC: nothing is written to the audit log unless the user has enabled it.
  Scenario: Audit log is off by default
    Given a fresh installation with default settings
    When a transcription completes
    Then no audit records are written

  # AC: after an explicit opt-in, an audit record is written locally and stays on the device.
  Scenario: Audit log writes only after explicit opt-in
    Given the user has explicitly enabled the audit log
    When a transcription completes
    Then an audit record is written to the local store
    And no data leaves the device

  # AC: disabling the audit log stops writes immediately (no restart).
  Scenario: Disabling the audit log stops writes immediately
    Given the user has explicitly enabled the audit log
    And a transcription completes
    When the user disables the audit log
    And a transcription completes
    Then the audit log contains exactly 1 record

  # AC: a user-initiated purge clears transcript history and the audit log from disk.
  Scenario: Purge clears history and the audit log
    Given the user has explicitly enabled the audit log
    And a transcription completes
    When the user purges their data
    Then no audit records are written
    And no transcript history remains
