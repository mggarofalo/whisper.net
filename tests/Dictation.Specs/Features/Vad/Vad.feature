# Coverage map (acceptance criterion -> scenario):
#  AC1 per-window speech-probability evaluation        -> "Speech is scored window by window"
#  AC2 on-device Silero ONNX (no egress)               -> OnnxVadSession (real model) -> manual smoke (see PR + follow-up)
#  AC3 all-silence segments gated out                  -> "A silent segment is gated out"
#  AC4 trailing trimmed; leading trimmed to preroll    -> "Trailing silence is trimmed" (+ leading: policy unit test)
#  AC5 long mid-utterance pause collapsed, speech kept -> "A long mid-utterance pause is collapsed"
#  AC6 thresholds configurable; model ships w/ the app -> thresholds: VadOptions (configured in scenarios + units);
#                                                         bundled model: follow-up issue (asset not yet committed)

@WHISPER-31
Feature: Voice activity gates and trims captured audio
  As the dictation pipeline
  I want silence detected and removed before transcription
  So that dead air is never sent to the model and recordings carry only speech

  Scenario: A silent segment is gated out
    Given a recording containing only silence
    When the recording is gated and trimmed by voice activity
    Then the recording is gated out as containing no speech

  Scenario: Trailing silence is trimmed
    Given a recording of 1 second of speech followed by 2 seconds of silence
    When the recording is gated and trimmed by voice activity
    Then the trimmed recording is 1 second long
    And the speech is preserved

  Scenario: A long mid-utterance pause is collapsed
    Given a recording of 1 second of speech, a 3 second pause, then 1 second of speech
    And the mid-silence collapse threshold is 1 second
    When the recording is gated and trimmed by voice activity
    Then the trimmed recording is 3 seconds long
    And both speech portions are preserved

  Scenario: Speech is scored window by window
    Given a recording of speech, silence, then speech
    When the recording is analyzed for voice activity
    Then voice activity is detected in the first and third windows but not the second
