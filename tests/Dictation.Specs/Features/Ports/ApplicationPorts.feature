@WHISPER-44
Feature: Application ports are mockable seams for behavior tests
  As the layer the BDD scenarios drive
  I want every Infrastructure dependency behind a substitutable port
  So that behavior can be validated without native libraries or real I/O

  # AC: every port is substitutable with NSubstitute and drives via its interface.
  Scenario: A port is driven through its interface using a substitute
    Given the transcriber port is replaced with a substitute that returns "hello world"
    When a transcription is requested through the port
    Then the caller receives the text "hello world"

  # AC: ports are expressed purely in Domain types / Application DTOs — no native or framework leakage.
  Scenario: Ports never expose native or framework types
    When the Application port method signatures are inspected
    Then no parameter or return type comes from a native or framework dependency
