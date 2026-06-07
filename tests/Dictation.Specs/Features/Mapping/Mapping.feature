@WHISPER-49
Feature: DTOs map losslessly to and from domain types
  As the Application layer
  I want compile-time generated mappers between DTOs and domain types
  So that the boundary stays free of hand-written, drift-prone mapping code

  # AC: bidirectional, total mapping with round-trip fidelity (transcript/history).
  Scenario: A transcript entry round-trips through its DTO unchanged
    Given a transcript entry domain object
    When it is mapped to a DTO and back to the domain type
    Then the round-tripped value equals the original

  # AC: bidirectional, total mapping with round-trip fidelity (settings).
  Scenario: Settings round-trip through their DTO unchanged
    Given an app-settings domain object
    When it is mapped to a DTO and back to the domain type
    Then the round-tripped value equals the original
