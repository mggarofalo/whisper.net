@WHISPER-32
Feature: Run on login
  The user can opt into launching the dictation app at login. Enabling registers the app under the
  current-user startup registration; disabling removes it. The state always reflects the real
  registration, and toggling is idempotent.

  Because the registration names a specific executable, it goes stale when the app is reinstalled to a
  different location — and Windows then launches nothing at login while the toggle still reads as enabled.
  So every launch re-asserts an enabled registration against the install that is actually running, without
  ever opting in a user who has not asked for it.

  Scenario Outline: Toggling run-on-login updates registration
    Given run-on-login is currently <initial>
    When the user sets run-on-login to <target>
    Then the startup registration is <expected>

    Examples:
      | initial  | target   | expected |
      | disabled | enabled  | present  |
      | enabled  | disabled | absent   |

  Scenario: A registration left behind by a removed install is repaired on launch
    Given run-on-login was registered by an install whose executable is gone
    When the app starts
    Then the startup registration points at this installation

  Scenario: A registration naming a different install is repointed on launch
    Given run-on-login was registered by an install whose executable is still present
    When the app starts
    Then the startup registration points at this installation

  Scenario: Launching never opts in a user who left run-on-login off
    Given run-on-login is currently disabled
    When the app starts
    Then the startup registration is absent
