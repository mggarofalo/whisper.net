# Coverage map (acceptance criterion -> scenario / test):
#  AC1 IAudioFeedback exposes start/stop/transcription-complete cues -> the Play(FeedbackSound) port + scenarios
#  AC2 Infrastructure plays a distinct bundled sound per cue         -> AudioFeedbackPlayer (smoke-only, like the capture client)
#  AC3 orchestrator fires feedback at each transition, never blocks  -> "Each pipeline event plays its sound"
#  AC4 configurable on/off; disabled => no sound, no resources       -> "Feedback is silent when disabled"
#  AC5 playback failure logged + swallowed, never breaks dictation   -> DictationOrchestratorTests.A_feedback_failure_does_not_break_dictation

@WHISPER-21
Feature: Audio feedback for dictation events
  As someone dictating without watching the screen
  I want a short sound at each pipeline event
  So that I can hear when recording starts, stops, and transcription is done

  Scenario Outline: Each pipeline event plays its sound when feedback is enabled
    Given audio feedback is enabled
    When the pipeline reaches "<event>"
    Then the "<event>" sound is played

    Examples:
      | event                  |
      | recording started      |
      | recording stopped      |
      | transcription complete |

  Scenario: Feedback is silent when disabled
    Given audio feedback is disabled
    When the pipeline reaches "recording started"
    Then no sound is played
