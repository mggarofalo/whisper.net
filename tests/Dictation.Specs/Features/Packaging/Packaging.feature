# Coverage map (acceptance criterion -> scenario / evidence):
#  AC1 self-contained single-file win-x64 publish with .NET 10 bundled
#         -> "The app publishes as a self-contained, single-file Windows build"
#            (demonstrated end-to-end by running build/pack.ps1 — see PR)
#  AC2 native assets (Whisper.net + Vulkan, ONNX, SharpHook) bundled and load at runtime
#         -> "...native assets are bundled" + demonstrated by running the published exe `--doctor`
#            (NAudio, Vulkan and SharpHook all loaded from the bundle — see PR)
#  AC3 `vpk pack` produces an installer with app id, version, icon
#         -> "The packaging script produces a Velopack installer reproducibly"
#            (Whisper.Net-win-Setup.exe + update package produced — see PR)
#  AC4 version flows from MinVer (git tag) — no hand-edited version strings
#         -> "The package version is derived from MinVer, not hand-edited"
#  AC5 documented one-command local packaging path reproducible from a clean clone
#         -> "...one-command packaging script" + pinned vpk tool; documented in docs/packaging.md
#  Launch on a machine with NO .NET runtime -> follow-up (clean-VM smoke test); the self-contained
#         bundle makes this true by construction and the exe runs on its bundled runtime here.

@WHISPER-20
Feature: Self-contained installer packaging
  As a maintainer cutting a release
  I want the WPF app packaged as a self-contained Velopack installer with a tag-derived version
  So that an end user installs and runs it without a separate .NET install

  Scenario: The app publishes as a self-contained, single-file Windows build
    Given the packaging configuration
    Then the Presentation project publishes self-contained for win-x64 as a single file
    And the native assets are kept loose for the runtime loader to find

  Scenario: The package version is derived from MinVer, not hand-edited
    Given the packaging configuration
    Then no static assembly version is committed
    And the version is derived from git tags by MinVer
    And the packaging script reads the version from MinVer rather than a literal

  Scenario: The packaging script produces a Velopack installer reproducibly
    Given the packaging configuration
    Then a one-command packaging script builds a Velopack installer
    And the app id and icon are set
    And the vpk tool is pinned for a reproducible build
