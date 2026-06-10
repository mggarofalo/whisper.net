# Coverage map (acceptance criterion -> scenario / test):
#  AC1 a long download shows determinate progress and a working Cancel; cannot run concurrently
#         -> "A running download shows progress and can be cancelled" +
#            Logic.AppManagement.Tests/Shell/ModelDownloadTests (IsRunning + non-concurrency)
#  AC2 the UI thread never blocks (no .Result/.Wait); progress renders smoothly
#         -> the command is async end-to-end (ModelDownloadTests drive it without blocking); the live
#            determinate percent is asserted in "A running download shows progress and can be cancelled"
#  AC3 on completion the selected model live-applies; failures surface a native error, not a crash
#         -> "A failed download surfaces a native error" + the @WHISPER-27 activate-on-success scenario
#            (live-apply is the WHISPER-78 instant-apply path SwitchActiveModel publishes on)

@WHISPER-81
Feature: Downloading a model shows progress and can be cancelled or fail gracefully
  As someone choosing a Whisper model
  I want a long download to show progress, let me cancel, and fail clearly
  So that the app stays responsive and I am never left guessing or crashed

  Scenario: A running download shows progress and can be cancelled
    Given the model list is loaded for download
    And the model "base.en" downloads slowly
    When the user starts downloading "base.en"
    Then the download for "base.en" is running with progress
    When the user cancels the download
    Then the download for "base.en" is reset and the model is not activated

  Scenario: A failed download surfaces a native error
    Given the model list is loaded for download
    And the model "base.en" download will fail
    When the user downloads "base.en" to completion
    Then a native download error is shown
    And the model "base.en" is not activated
