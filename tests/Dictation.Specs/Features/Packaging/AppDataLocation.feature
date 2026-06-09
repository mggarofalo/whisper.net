# Coverage map (acceptance criterion -> scenario / evidence):
#  AC1 mutable data (model cache, logs) lives in a directory that does NOT collide with the Velopack
#         install dir (PackId) -> "Per-user data directories never collide with the Velopack install root"
#  AC2 installing/updating over a running app no longer fails on a locked log handle inside the install
#         dir (the open log file is outside the install root) -> same scenario asserts the logs directory
#         is outside the install root
#  AC3 the model cache + logs + settings DB paths are consistent and documented (dev re-download
#         acceptable) -> docs/packaging.md "Per-user data locations" + CHANGELOG (see PR)
#  AC4 a test pins the data-directory resolution so it can never again equal the PackId/install path
#         -> Infrastructure.Tests/Startup/WhisperAppDataTests + this scenario

@WHISPER-86
Feature: Per-user data lives outside the Velopack install directory
  As a maintainer shipping the tray app through a Velopack installer
  I want the model cache, logs, and settings database to live outside the install root
  So that installing or updating over a running app never touches or locks user data

  Scenario: Per-user data directories never collide with the Velopack install root
    When the application resolves its per-user data directories
    Then the data-root folder name is not the Velopack pack id
    And the logs directory is outside the Velopack install root
    And the model cache directory is outside the Velopack install root
    And the settings database is outside the Velopack install root
