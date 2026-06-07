# Coverage map (acceptance criterion -> scenario):
#  AC1 bracketed/parenthesized noise labels stripped (incl. brackets) -> "Strip bracketed noise labels"
#  AC2 fillers removed on word boundaries, trailing punctuation eaten  -> "Remove filler words when enabled" (outline)
#  AC3 leading filler leaves no stranded space/punctuation, trimmed    -> "Remove filler words when enabled" (Um,/Ummm rows)
#  AC4 filler removal gated by setting; noise stripping always on      -> "Filler words are kept ..." + "Noise labels are stripped even ..."
#  AC5 side-effect-free and idempotent                                 -> FillerWordCleaner unit tests (idempotence)

@WHISPER-36
Feature: Transcription normalization
  Raw Whisper output is normalized before delivery: bracketed and parenthesized noise labels are
  always stripped, and spoken filler words are removed only when the user has enabled that setting.

  Scenario: Strip bracketed noise labels
    Given a raw transcription "Hello [BLANK_AUDIO] world"
    When the transcription is normalized
    Then the normalized text is "Hello world"

  Scenario: Noise labels are stripped even when filler removal is off
    Given the "remove filler words" setting is off
    And a raw transcription "Um [SILENCE] keep this"
    When the transcription is normalized
    Then the normalized text is "Um keep this"

  Scenario Outline: Remove filler words when enabled
    Given the "remove filler words" setting is on
    And a raw transcription "<input>"
    When the transcription is normalized
    Then the normalized text is "<output>"

    Examples:
      | input              | output          |
      | Um, I think so     | I think so      |
      | So uh we should go | So we should go |
      | Hmm let me check   | let me check    |
      | Ummm, okay then    | okay then       |

  Scenario: Filler words are kept when the setting is off
    Given the "remove filler words" setting is off
    And a raw transcription "Um, I think so"
    When the transcription is normalized
    Then the normalized text is "Um, I think so"
