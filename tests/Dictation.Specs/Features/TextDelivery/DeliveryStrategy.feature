@WHISPER-8
Feature: Delivery-strategy selection
  As someone who dictates into different kinds of apps
  I want to choose how text is delivered, and override it per delivery
  So that the right mechanism is used without my having to think about it

  Scenario Outline: Configured default strategy is applied
    Given the configured delivery strategy is "<configured>"
    And no per-delivery override is supplied
    And the model will transcribe the audio to "send the email"
    When a transcription is delivered
    Then the "<configured>" delivery path is used

    Examples:
      | configured |
      | Type       |
      | Paste      |

  Scenario: Per-delivery override takes precedence over the default
    Given the configured delivery strategy is "Type"
    And a per-delivery override of "Paste" is supplied
    And the model will transcribe the audio to "send the email"
    When a transcription is delivered
    Then the "Paste" delivery path is used
