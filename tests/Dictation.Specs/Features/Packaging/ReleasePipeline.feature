# Coverage map (acceptance criterion -> scenario):
#  AC1 CI triggers only on tags matching v*.*.*        -> "The release runs only on a version tag"
#  AC2 restore, build -warnaserror, test; fail blocks  -> "A failing build blocks the release"
#  AC3 MinVer derives version from the tag (assemblies + package)
#                                                       -> "A pushed tag publishes a versioned GitHub Release"
#  AC4 Velopack artifacts published to a GitHub Release -> "A pushed tag publishes a versioned GitHub Release"
#  AC5 signing secrets injected from secrets, not echoed
#                                                       -> "Signing secrets come from GitHub Actions secrets ..."
#  Cutting the first live release by pushing a tag (outward-facing) -> follow-up.

@WHISPER-39
Feature: Tag-driven release pipeline
  As a maintainer
  I want pushing a vX.Y.Z tag to build, package, and publish a release
  So that cutting a release is a single git tag + push, and a broken build never ships

  Scenario: The release runs only on a version tag
    Given the release workflow
    Then it triggers only on tags matching a version pattern
    And it does not run on pull requests or branch pushes

  Scenario: A failing build blocks the release
    Given the release workflow
    Then it builds with warnings-as-errors and runs the tests
    And it builds and tests before it packages or publishes
    And no build or test step is allowed to continue on error

  Scenario: A pushed tag publishes a versioned GitHub Release
    Given the release workflow
    Then the version is derived from the tag by MinVer
    And it packages the installer with Velopack
    And it publishes the installer and update package to a GitHub Release

  Scenario: Signing secrets come from GitHub Actions secrets and are never echoed
    Given the release workflow
    Then code-signing secrets are injected from GitHub Actions secrets
    And no secret is echoed to the log
