# Coverage map (acceptance criterion -> scenario / test):
#  AC1 a doctor entry point runs all checks and prints a pass/warn/fail report
#         -> entry point: DoctorModeTests (arg routing) + DiagnosticReportFormatterTests (the printed report);
#            "all checks run" proven by "All subsystems healthy" + "Each diagnostic yields a structured ..."
#  AC2 audio check confirms a usable capture device       -> "A required subsystem is unavailable" <Audio>
#  AC3 model check reports whether the model is cached     -> "A required subsystem is unavailable" <Model>
#  AC4 hotkey check confirms the hotkey can be registered  -> "A required subsystem is unavailable" <Hotkey>
#  AC5 GPU check reports Vulkan, falling back to CPU report rather than erroring
#         -> "GPU absence is reported as a CPU fallback, not a failure"
#  AC6 each check yields a structured (name, status, detail) result
#         -> "Each diagnostic yields a structured name, status, and detail"

@WHISPER-50
Feature: Environment self-diagnostics
  As a user diagnosing a problem (or attaching a health snapshot to a bug report)
  I want a one-command check of the app's subsystems
  So that I can see at a glance what works and what needs attention

  Scenario: All subsystems healthy
    Given a capture device is available
    And the configured model is downloaded
    And the input permission required for the hotkey is granted
    And a Vulkan GPU runtime is available
    When the diagnostics run
    Then every diagnostic reports a passing status

  Scenario Outline: A required subsystem is unavailable
    Given every subsystem is healthy
    But the "<subsystem>" subsystem is unavailable
    When the diagnostics run
    Then the "<subsystem>" check reports a failing status
    And every subsystem still produces a result

    Examples:
      | subsystem |
      | Audio     |
      | Model     |
      | Hotkey    |

  Scenario: GPU absence is reported as a CPU fallback, not a failure
    Given every subsystem is healthy
    But no Vulkan GPU runtime is available
    When the diagnostics run
    Then the "GPU" check does not report a failing status
    And the "GPU" check detail mentions the CPU backend

  Scenario: Each diagnostic yields a structured name, status, and detail
    Given every subsystem is healthy
    When the diagnostics run
    Then each diagnostic has a name, a status, and a non-empty detail
