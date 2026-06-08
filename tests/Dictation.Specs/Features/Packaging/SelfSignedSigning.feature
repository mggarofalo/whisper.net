# Coverage map (acceptance criterion -> scenario / evidence):
#  AC1 a reproducible script generates a self-signed code-signing cert and emits the base64 PFX +
#      password that pack.ps1 consumes; no cert/key is committed
#         -> "A reproducible script provisions a self-signed signing certificate"
#  AC2 the script can optionally trust the cert locally so signtool verify /pa passes
#         -> "The signing script can trust the certificate for local verification"
#  AC3 a build-and-run guide documents build-from-source + self-signed signing + running, linked from
#      the README, with the honest SmartScreen caveat
#         -> "Building from source is documented and linked from the README"

@WHISPER-72
Feature: Self-signed code signing for personal builds
  As a maintainer of a personal project
  I want a reproducible self-signed signing path and a build-from-source guide
  So that I can produce a signed installer and trusted local installs without buying a CA certificate

  Scenario: A reproducible script provisions a self-signed signing certificate
    Given the packaging configuration
    Then a self-signed signing script emits a base64 PFX and password for pack.ps1
    And the signing script commits no certificate or private key

  Scenario: The signing script can trust the certificate for local verification
    Given the packaging configuration
    Then the signing script can trust the certificate in the local store for signtool verification

  Scenario: Building from source is documented and linked from the README
    Given the repository documentation
    Then a build-and-run guide documents building from source, self-signed signing, and running
    And the build-and-run guide is honest that self-signed signing does not bypass SmartScreen
    And the README links to the build-and-run guide
