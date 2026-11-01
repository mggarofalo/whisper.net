# Coverage map (acceptance criterion -> scenario / evidence):
#  AC1 the General page shows the running version plus a short commit hash
#         -> "The General page shows the running version" (outline)
#  AC2 nothing touches the network until the user clicks Check for updates; the click reports the result
#         -> "Opening the section never checks for updates", "Checking finds a newer release",
#            "Checking when already up to date", "A failed check is reported and the app keeps running"
#  AC3 a build that is not a Velopack install says so and makes no request
#         -> "A build that is not installed cannot self-update"
#  AC4 Download and restart downloads, stages, and shuts down gracefully; a failed download keeps running
#         -> "Installing an available update downloads it and restarts the app",
#            "A failed download leaves the app running on the current version"
#  AC5 automatic checks are a separate opt-in, persisted, off by default
#         -> "Automatic update checks are off by default", "Turning on automatic checks persists and drives
#            the startup check"
#  AC6 README + CHANGELOG disclosure -> documentation (reviewed in the PR)
#  An end-to-end update of an installed build against a published release is environmental (manual).

@WHISPER-151
Feature: Version display and in-app updates
  As a user
  I want to see which version I am running and update it from inside the app
  So that I can get fixes without hunting for an installer — and nothing contacts the network unless I ask

  Scenario Outline: The General page shows the running version
    Given the running build's informational version is "<informational>"
    When the user opens the General settings
    Then the version shown is "<shown>"

    Examples:
      | informational                                            | shown                   |
      | 0.5.3+c9efc2f3a7b1d9e0f00112233445566778899aab           | 0.5.3+c9efc2f           |
      | 0.5.4-alpha.0.2+0123456789abcdef0123456789abcdef01234567 | 0.5.4-alpha.0.2+0123456 |
      | 0.5.3                                                    | 0.5.3                   |

  Scenario: Opening the section never checks for updates
    Given the app is an installed build
    When the user opens the General settings
    Then no update check is made

  Scenario: Checking finds a newer release
    Given the app is an installed build
    And release "0.6.0" is published
    And the user opens the General settings
    When the user checks for updates
    Then the update status reads "Version 0.6.0 is available."
    And the user can download and restart

  Scenario: Checking when already up to date
    Given the app is an installed build
    And no newer release is published
    And the user opens the General settings
    When the user checks for updates
    Then the update status reads "Whisper is up to date."
    And the user cannot download and restart

  Scenario: A failed check is reported and the app keeps running
    Given the app is an installed build
    And the release feed cannot be reached
    And the user opens the General settings
    When the user checks for updates
    Then the update status reads "Couldn't check for updates: the release feed is unreachable."
    And the user cannot download and restart

  Scenario: A build that is not installed cannot self-update
    Given the app is not an installed build
    And the user opens the General settings
    When the user checks for updates
    Then the update status reads "Updates are only available in the installed app."
    And no update check is made

  Scenario: Installing an available update downloads it and restarts the app
    Given the app is an installed build
    And release "0.6.0" is published
    And the user opens the General settings
    And the user checks for updates
    When the user chooses to download and restart
    Then update "0.6.0" is downloaded and staged to apply on restart
    And the app shuts down so the update can be applied

  Scenario: A failed download leaves the app running on the current version
    Given the app is an installed build
    And release "0.6.0" is published
    And downloading the update fails
    And the user opens the General settings
    And the user checks for updates
    When the user chooses to download and restart
    Then the update status reads "Couldn't install the update: the download was interrupted."
    And the app keeps running

  Scenario: Automatic update checks are off by default
    When the user opens the General settings
    Then automatic update checks are off
    And at startup no update check is made

  Scenario: Turning on automatic checks persists and drives the startup check
    Given the app is an installed build
    And release "0.6.0" is published
    And the user opens the General settings
    When the user turns on automatic update checks
    Then automatic update checks are persisted as on
    And at startup update "0.6.0" is downloaded and staged to apply
