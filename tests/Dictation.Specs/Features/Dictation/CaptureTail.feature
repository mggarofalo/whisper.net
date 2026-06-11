# Coverage map (acceptance criterion -> scenario):
#  AC1 audio still in flight at chord release is captured into the clip      -> "A short dictation transcribes audio captured after chord release"
#  AC2 quiet trailing speech survives the trim; sustained dead air does not  -> "Quiet trailing speech is preserved by the trimmer",
#                                                                               "Sustained quiet trailing speech is preserved by the trimmer",
#                                                                               "Genuine trailing dead air is trimmed"
#  AC3 unit coverage for the grace-window drain and the sustained-silence    -> DictationOrchestratorTests (Logic.AppManagement.Tests)
#      trim                                                                     + SilenceTrimmerTests (Logic.AudioManagement.Tests)

@WHISPER-112
Feature: Capture tail after chord release
  As someone dictating right up to the moment I release the hotkey
  I want the audio still in flight at release to be captured and kept
  So that the end of my sentence is transcribed instead of cut off

  # The real device's stop is asynchronous: NAudio keeps delivering the frames already in flight (the
  # user's final syllables) for a short moment after the stop request. The orchestrator must keep the
  # capture open through a post-release grace window so that tail lands in the delivered clip.
  Scenario: A short dictation transcribes audio captured after chord release
    Given the user is dictating a short phrase
    When the user releases the chord
    And the device delivers the remaining audio during the grace window
    And the post-release grace window elapses
    Then the clip handed to the transcriber contains the post-release audio

  # A short quiet tail is usually the soft end of speech (a trailing "s", a breathy consonant), not
  # dead air — trimming it swallows the word ending the user actually spoke.
  Scenario: Quiet trailing speech is preserved by the trimmer
    Given a clip that ends in 100 ms of quiet trailing speech
    When trailing silence is trimmed
    Then the quiet trailing speech is preserved

  # The reopen: a SUSTAINED quiet tail (longer than the silence window) was wrongly cut by the per-sample
  # amplitude bar even though it carries real speech energy. Energy-aware end-of-speech keeps it.
  Scenario: Sustained quiet trailing speech is preserved by the trimmer
    Given a clip that ends in 400 ms of quiet trailing speech
    When trailing silence is trimmed
    Then the quiet trailing speech is preserved

  Scenario: Genuine trailing dead air is trimmed
    Given a clip that ends in 300 ms of dead air
    When trailing silence is trimmed
    Then the dead air is trimmed away leaving only a short pad
