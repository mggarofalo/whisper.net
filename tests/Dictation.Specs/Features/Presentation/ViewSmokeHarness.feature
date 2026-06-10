# Coverage map (acceptance criterion -> scenario / test):
#  AC1 An STA smoke test instantiates each feature view with its VM and asserts construction + first
#      bind without throwing (binding errors fail the test)
#         -> "The smoke harness constructs and binds every feature view on an STA thread"
#            (artifact: the Presentation.Smoke.Tests project exists, is STA-threaded, captures
#            data-binding trace errors and fails on them; the tests themselves are the proof and run
#            in the same fast gate as this scenario)
#  AC2 Every registered NavigationSection VM type has a matching DataTemplate (a missing template
#      fails a test)
#         -> "The smoke harness fails when a section is missing its data template" (artifact: the
#            completeness test enumerates the real registered sections against the shell's resources)
#  AC3 A note records the FlaUI adopt-vs-defer decision with rationale; CI runs the smoke layer on
#      Windows
#         -> "The FlaUI decision is recorded with rationale"
#         -> "CI runs the smoke layer on Windows" (the smoke project is in the solution and CI's
#            Windows test step gates the whole solution)

@WHISPER-96
Feature: A thin STA smoke layer guards the view glue
  As a developer relying on WPF-free view-model tests
  I want a minimal view-level smoke harness for the bindings and templates
  So that a binding-path typo or a missing data template fails a test instead of silently breaking the UI

  Scenario: The smoke harness constructs and binds every feature view on an STA thread
    Then an sta smoke project constructs each feature view against its view-model
    And the smoke harness fails on data-binding errors

  Scenario: The smoke harness fails when a section is missing its data template
    Then the smoke harness checks every registered section for a matching data template

  Scenario: The FlaUI decision is recorded with rationale
    Then the testing guide records the flaui adopt-versus-defer decision

  Scenario: CI runs the smoke layer on Windows
    Then the smoke project is part of the solution gated by the windows ci test step
