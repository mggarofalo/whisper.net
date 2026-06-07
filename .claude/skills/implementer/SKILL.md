---
name: implementer
description: Drive the inner TDD red-green-refactor loop for a WHISPER issue until its @WHISPER-<id> Reqnroll scenarios pass. Use after spec-author has produced failing tagged scenarios and you need production code (Application handlers + Logic.*) and supporting unit tests written test-first. Triggers: "implement WHISPER-<id>", "make the scenarios green", "drive the inner TDD loop".
---

# implementer

You take a WHISPER issue whose `@WHISPER-<id>` scenarios are **failing** (authored by
[spec-author](../spec-author/SKILL.md)) and drive the **inner TDD loop** — RED → GREEN → REFACTOR —
until those scenarios pass. This is the "inside" of the double loop in
[`docs/bdd-strategy.md`](../../../docs/bdd-strategy.md): the failing acceptance scenario is the goal;
xUnit unit tests are the scaffolding you build to reach it.

You write **real production code** and **real unit tests**. You write the step bindings and drivers
the scenarios need. You never weaken the acceptance scenarios to make them pass.

## Inputs

- A WHISPER issue id whose tagged scenarios already exist and are red.

## Procedure

1. **Confirm the starting red.** Run `dotnet test tests/Dictation.Specs --filter "Category=WHISPER-<id>"`
   and see the scenarios fail (typically undefined steps). Read the issue's acceptance criteria so you
   know what "done" means behaviorally.

2. **Wire the outer loop's bindings.** Add the thin step definitions and a Driver (the headless
   Page-Object equivalent — see `docs/bdd-strategy.md` §3) so the scenario sends an
   `ICommand<T>`/`IQuery<T>` through `IMediator` and asserts at the result or port boundary. Step
   definitions stay one-liners; mechanics live in the Driver. Now the scenario fails for the *right*
   reason: there is no handler / no behavior yet.

3. **Drive the inner loop, one behavior at a time.** For each piece of logic the scenario needs:
   - **RED** — write a failing xUnit test (in the matching `tests/<Layer>.Tests` project) for one
     edge case or branch of the unit you're about to build.
   - **GREEN** — write the *minimum* production code to pass it.
   - **REFACTOR** — clean up with the test still green.
   Repeat until the handler and the `Logic.*` pieces it orchestrates exist.

4. **Close the outer loop.** Re-run the tagged scenarios. When the production code is in place they go
   green. Then refactor across units with everything still green.

## Layering rules (non-negotiable — enforced by `tests/Architecture.Tests`)

- Put code in the **correct layer**: orchestration in **Application handlers**; deterministic
  behavior/math in **`Logic.*`**; I/O behind **Infrastructure ports**. See
  [`docs/architecture.md`](../../../docs/architecture.md) and
  [`docs/coding-standards.md`](../../../docs/coding-standards.md).
- **Never fake `Logic.*`.** In specs, only **Infrastructure ports** are substituted (NSubstitute);
  real `Logic.*` and real handlers run. Faking Logic means the scenario tests nothing.
- Handlers depend on **Application-declared abstractions** only — never a concrete Infrastructure or
  `Logic.*` type. Reuse the production `AddX` registration extensions; do not build parallel wiring.
- CQRS via the source-generated **Mediator** (not MediatR); mapping via **Mapperly**; validation via
  **FluentValidation** in the pipeline, not the handler.

## Unit tests: meaningful, not box-checking

- Write unit tests for **edge cases and branches** the scenario doesn't exercise directly (empty
  input, boundary thresholds, cancellation, error paths). These are the depth behind the scenario's
  breadth.
- **Prefer no test over a box-checking test.** Do not assert that a mock was called, restate a handler
  line-by-line, or test generated Mapperly internals. If you can't name the behavior a test protects,
  delete it.
- Use **AwesomeAssertions** (`.Should()`) and **NSubstitute**; assert on outcomes, not internal calls
  (the one acceptable interaction assertion is at the **port boundary**).

## Stop-and-report rule

If you **cannot make a scenario pass**, stop and report why — the blocker, what you tried, and what's
needed. **Never**:
- tag a scenario `@wip` to dodge the CI gate,
- delete/soften the scenario or comment out an assertion,
- mark the issue progressed.
A red scenario you can't turn green is a finding to surface, not a gate to bypass.

## Definition of done (for this skill's output)

- The issue's `@WHISPER-<id>` scenarios are **green** under `dotnet test`.
- Supporting unit tests for the new logic are green and meaningful.
- Production code sits in the correct layers; no `Logic.*` was faked; `tests/Architecture.Tests`
  stays green.
- `dotnet build -warnaserror` and `dotnet format --verify-no-changes` are clean.

## Handoff

Report the green scenario list, the new/changed production files by layer, and the supporting unit
tests. The [dod-validator](../dod-validator/SKILL.md) verifies AC coverage before Done.
