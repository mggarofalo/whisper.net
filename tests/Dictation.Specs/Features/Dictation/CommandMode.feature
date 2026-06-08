# Coverage map (acceptance criterion -> scenario / test):
#  AC1 ICommandMatcher port returns matched-vs-no-match           -> ICommandMatcher + CommandMatch (used by both scenarios)
#  AC2 matcher invoked after transcription, before delivery; match routes away, else delivers
#                                                                  -> both scenarios
#  AC3 no-op default returns "no match", behavior unchanged        -> NoOpCommandMatcherTests + "Unmatched speech falls through"
#  AC4 fake match diverts from delivery; no-match preserves it     -> both scenarios
#  AC5 hook + abstraction only (no parsing/catalogue/execution)    -> scope: scenarios assert routing only, no command is run

@WHISPER-35
Feature: Command-mode hook for transcribed speech
  As the dictation pipeline
  I want a transcript to be matched against user-defined commands before it is typed
  So that a recognized command can be routed to a command branch instead of delivered as text

  Scenario: A matched command is routed away from text delivery
    Given a command matcher that recognizes the transcript as a command
    When an utterance is transcribed
    Then the command branch is invoked
    And the transcript is not delivered as text

  Scenario: Unmatched speech falls through to normal delivery
    Given a command matcher that recognizes no command
    When an utterance is transcribed
    Then the transcript is delivered as text
