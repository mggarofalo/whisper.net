# Coverage map (acceptance criterion -> scenario):
#  AC1 port: start/stop, frame events, exposed capture format -> "Captured frames are delivered ..."
#  AC2 negotiate device mix format, surface via port          -> "Captured frames are delivered ..." (format surfaced)
#  AC3 idempotent start; stop flushes pending + releases      -> "Starting an already-running ...",
#                                                                "Stopping flushes pending frames ..."
#  AC5 device errors surface as a typed failure, no throw     -> "A device error is reported ..."
#  AC4 capture off the caller's thread / non-blocking         -> real-NAudio thread affinity: manual smoke (see PR)
#  AC6 registered in Infrastructure DI                        -> Hosting.Tests host-composition test (see PR)

@WHISPER-7
Feature: Microphone capture behind IAudioSource
  As the dictation pipeline
  I want microphone audio delivered as frames in a known format
  So that recordings can be normalized and transcribed without touching the device directly

  Scenario: Captured frames are delivered in the negotiated format after start
    Given a capture device producing 48000 Hz stereo float audio
    When capture starts
    And the device produces a frame of 512 samples
    Then a frame of 512 samples is delivered in the negotiated 48000 Hz stereo format

  Scenario: Starting an already-running source does not restart capture
    Given a capture device producing 48000 Hz stereo float audio
    And capture has started
    When capture starts again
    Then the device is started only once

  Scenario: Stopping flushes pending frames and releases the device
    Given a capture device producing 48000 Hz stereo float audio
    And the device has 2 frames buffered to flush on stop
    And capture has started
    When capture stops
    Then the 2 buffered frames are delivered
    And no further frames are delivered afterward
    And the capture device is released

  Scenario: A device error is reported as a capture failure rather than thrown
    Given a capture device producing 48000 Hz stereo float audio
    And capture has started
    When the capture device becomes unavailable
    Then a capture failure is reported with reason "DeviceUnavailable"
    And no error is raised to the caller
