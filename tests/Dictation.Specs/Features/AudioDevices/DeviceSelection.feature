# Coverage map (acceptance criterion -> scenario):
#  AC1 enumerate active devices + identify default -> exercised in every scenario (enumerator supplies devices+default);
#                                                     real NAudio enumeration -> manual smoke (see PR)
#  AC2 selection persists + restored on next launch -> "A selected device persists across restarts"
#  AC3 "system default" sentinel follows the OS default -> "Following the default hot-swaps ..." (selection = follow default)
#  AC4 default change hot-swaps capture                 -> "Following the default hot-swaps when the default device changes"
#  AC5 missing device falls back to default + reports   -> "A missing selected device falls back to the system default"
#  AC6 enumeration/selection/notification wired in DI   -> Hosting.Tests host-composition tests (see PR)

@WHISPER-13
Feature: Capture device selection, persistence, and hot-swap
  As someone dictating
  I want to choose my microphone, have it remembered, and not lose dictation when devices change
  So that recording uses the right device across restarts and OS default changes

  Scenario: A selected device persists across restarts
    Given the capture devices "Mic A" and "Mic B" are available with "Mic A" as the default
    And the user selects capture device "Mic B"
    When the application restarts
    Then capture uses device "Mic B"

  Scenario: Following the default hot-swaps when the default device changes
    Given the capture devices "Mic A" and "Mic B" are available with "Mic A" as the default
    And the user follows the system default capture device
    When the system default capture device changes to "Mic B"
    Then capture uses device "Mic B"

  Scenario: A missing selected device falls back to the system default
    Given the capture devices "Mic A" and "Mic B" are available with "Mic A" as the default
    And the user has selected a capture device that is no longer present
    When capture resolves the device to use
    Then capture uses device "Mic A"
    And the device substitution is reported
