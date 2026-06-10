# Coverage map (acceptance criterion -> scenario / test):
#  AC1 the picker lists active capture devices by friendly name; selection commits and live-applies
#         -> "The picker lists devices by name and a selection commits"
#            (live-apply is the WHISPER-78 instant-apply path the UpdateSettings commit publishes on)
#  AC2 a persisted device that is no longer present surfaces a clear fallback/validation state, not a crash
#         -> "A removed device falls back to the system default with a warning"
#  AC3 device-list logic is testable behind an interface (no WPF in Logic.AppManagement)
#         -> this whole feature drives the WPF-free AudioDeviceViewModel over IMediator +
#            Logic.AppManagement.Tests/Shell/AudioDeviceViewModelTests

@WHISPER-80
Feature: Choosing the capture device from an enumerated picker
  As someone setting up dictation
  I want to pick my microphone from a list of real devices
  So that recording uses the right input and a missing device fails clearly, not silently

  Scenario: The picker lists devices by name and a selection commits
    Given two capture devices are available
    When the device list is loaded
    Then the picker lists "Microphone A" and "Microphone B"
    When the user picks the device "mic-b"
    Then the device "mic-b" is committed

  Scenario: A removed device falls back to the system default with a warning
    Given two capture devices are available
    And the saved capture device "ghost-mic" is no longer present
    When the device list is loaded
    Then the picker falls back to the system default
    And a clear unavailable-device warning is shown
