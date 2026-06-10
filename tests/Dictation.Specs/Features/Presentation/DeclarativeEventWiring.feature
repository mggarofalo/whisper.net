# Coverage map (acceptance criterion -> scenario / test):
#  AC1 No view subscribes to control events in code-behind for VM behavior; uses Interaction.Triggers
#      or a named attached behavior
#         -> "Views wire no control events to logic outside the sanctioned input controls"
#            (XAML event-handler attributes and code-behind subscriptions are scanned; only the
#            self-contained input controls under Shell/Controls may adapt raw input events)
#  AC2 Focus-on-activate is a reusable attached behavior, not a per-view Loaded handler
#         -> "Focus-on-activate is one reusable behavior applied declaratively"
#  AC3 Microsoft.Xaml.Behaviors.Wpf referenced; a "behavior vs command vs legitimate code-behind"
#      guideline committed
#         -> "The behaviors library is referenced for all views"
#         -> "The event-wiring guideline is committed" (including the InvokeCommandAction
#            does-not-honor-CanExecute caveat)

@WHISPER-93
Feature: View event wiring is declarative behaviors and commands, not code-behind
  As a developer evolving the WPF shell
  I want view-level event wiring expressed as commands and reusable attached behaviors
  So that no per-view code-behind accumulates untestable event-handler logic

  Scenario: Views wire no control events to logic outside the sanctioned input controls
    Then no view outside the input controls wires events in markup or code-behind

  Scenario: Focus-on-activate is one reusable behavior applied declaratively
    Then a reusable focus-on-activate behavior exists
    And at least one feature view applies it through interaction behaviors
    And no view carries a per-view loaded handler

  Scenario: The behaviors library is referenced for all views
    Then the presentation project references the xaml behaviors library

  Scenario: The event-wiring guideline is committed
    Then the architecture guide records the behavior-versus-command guideline
