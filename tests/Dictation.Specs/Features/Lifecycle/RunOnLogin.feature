@WHISPER-32
Feature: Run on login
  The user can opt into launching the dictation app at login. Enabling registers the app under the
  current-user startup registration; disabling removes it. The state always reflects the real
  registration, and toggling is idempotent.

  Scenario Outline: Toggling run-on-login updates registration
    Given run-on-login is currently <initial>
    When the user sets run-on-login to <target>
    Then the startup registration is <expected>

    Examples:
      | initial  | target   | expected |
      | disabled | enabled  | present  |
      | enabled  | disabled | absent   |
