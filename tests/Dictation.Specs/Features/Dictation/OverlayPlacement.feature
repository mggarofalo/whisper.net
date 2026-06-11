# Coverage map (acceptance criterion -> scenario):
#  AC1 overlay horizontally centered and bottom-anchored above the taskbar
#         -> "Centered horizontally and anchored above the taskbar..." (origin 0,0 row)
#  AC2 correct on multi-monitor (and high-DPI) setups
#         -> the offset-origin rows (a monitor to the right/left/above); on-screen DPI verification is the
#            manual remainder
#  AC3 position stays correct when the work area changes (taskbar moved/resized)
#         -> placement is a pure function of the work area, so any work-area rect re-centers/re-anchors;
#            the transient overlay recomputes this every time it is shown, so a work-area change between
#            recordings is reflected on the next show (a change during an active recording is the manual
#            remainder)

@WHISPER-100
Feature: The dictation overlay sits at the bottom-center of the work area
  As someone dictating
  I want the recording overlay anchored bottom-center, above the taskbar
  So that it does not cover the field I am dictating into

  Scenario Outline: Centered horizontally and anchored above the taskbar, honoring the work-area origin
    Given a work area at <x>,<y> sized <w> by <h>
    And the dictation overlay is <ow> by <oh>
    When the overlay is positioned
    Then the overlay is horizontally centered in the work area
    And the overlay is anchored 24 above the bottom of the work area
    And the overlay stays within the work area

    Examples:
      | x     | y     | w    | h    | ow  | oh |
      | 0     | 0     | 1920 | 1040 | 208 | 44 |
      | 1920  | 0     | 2560 | 1400 | 208 | 44 |
      | -1920 | 0     | 1920 | 1020 | 208 | 44 |
      | 0     | -1080 | 1920 | 1040 | 208 | 44 |
