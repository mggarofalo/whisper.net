# Coverage map (acceptance criterion -> scenario / test):
#  AC1 normal volume drives the meter to mid-range
#         -> "Normal-volume speech drives the meter to mid-range"
#  AC2 silence sits at/near zero; loud approaches full without pegging
#         -> "Silence keeps the meter near zero" + "Loud speech approaches full scale without pegging"
#  AC3 unit tests cover the RMS-to-display mapping
#         -> Logic.AppManagement.Tests/LevelOverlayControllerTests (ToPerceptualLevel theory + bands)

@WHISPER-101
Feature: The recording overlay meter uses a perceptual (dB) scale
  As someone dictating
  I want the level meter to track loudness perceptually, not as raw RMS
  So that normal speech visibly fills the bar instead of barely moving it

  Scenario: Normal-volume speech drives the meter to mid-range
    Given recording is underway
    When the microphone receives sustained normal-volume speech
    Then the overlay meter sits in the mid-range

  Scenario: Silence keeps the meter near zero
    Given recording is underway
    When the microphone receives sustained near-silence
    Then the overlay meter sits at or near zero

  Scenario: Loud speech approaches full scale without pegging
    Given recording is underway
    When the microphone receives sustained loud speech
    Then the overlay meter approaches full scale without pegging
