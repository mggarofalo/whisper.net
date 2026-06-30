@WHISPER-134
Feature: Start-at-login toggle
  The General settings section lets the user opt into launching Whisper at login, so after a reboot
  the app is already running in the tray instead of needing a manual relaunch. The toggle always
  reflects the real OS startup registration, and flipping it applies the change through that same
  registration.

  Scenario Outline: The toggle reflects the current registration when the section opens
    Given run-on-login is currently <state>
    When the user opens the General settings section
    Then the start-at-login toggle is <toggle>

    Examples:
      | state    | toggle |
      | enabled  | on     |
      | disabled | off    |

  Scenario Outline: Flipping the toggle applies through the startup registration
    Given run-on-login is currently <initial>
    And the user opens the General settings section
    When the user turns the start-at-login toggle <action>
    Then the startup registration is <expected>

    Examples:
      | initial  | action | expected |
      | disabled | on     | present  |
      | enabled  | off    | absent   |
