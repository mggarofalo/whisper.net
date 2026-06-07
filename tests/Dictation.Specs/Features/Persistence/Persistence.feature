@WHISPER-11
Feature: SQLite persistence store
  Settings and transcription history are persisted in a single local SQLite database whose schema is
  versioned (SQLite's user_version) and migrated forward on first use. A fresh database is created at the
  latest schema version with write-ahead logging enabled, an older database is migrated forward without
  losing data, re-running the migrations is a no-op, and a corrupt database recovers to defaults without
  crashing the host.

  # AC: schema is created on first run; the file lives at the configured path; WAL mode is used.
  Scenario: First run creates the schema with write-ahead logging
    Given no database file exists
    When the persistence store initializes
    Then a database is created at the configured path
    And its schema version equals the latest known version
    And write-ahead logging is enabled

  # AC: an ordered migration runner applies pending migrations forward without losing user data.
  Scenario: An existing database is migrated forward without losing data
    Given a database at an older schema version holding a transcript entry
    When the persistence store initializes
    Then the pending migrations are applied
    And its schema version equals the latest known version
    And the existing transcript entry is preserved

  # AC: running the migrations against an up-to-date database is idempotent (a no-op).
  Scenario: Re-running the migrations is a no-op
    Given a database already at the latest schema version
    When the persistence store initializes again
    Then no migration runs
    And its schema version equals the latest known version

  # AC: a bad/partially-written database fails safe with a logged error rather than crashing the host.
  Scenario: A corrupt database recovers to defaults
    Given the database file is corrupt
    When settings are loaded from the store
    Then default settings are returned
    And the store logs the recovery
