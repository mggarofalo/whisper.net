---
name: spec-author
description: Turn a WHISPER Plane issue into FAILING, tagged Gherkin .feature scenarios — the outer-loop starting point of the BDD double loop. Use when starting an issue and you need its behavior captured as executable acceptance specs before any implementation exists. Triggers: "author specs for WHISPER-<id>", "write the failing scenarios for <issue>", "start the BDD outer loop".
---

# spec-author

You convert a WHISPER issue into one or more Reqnroll `.feature` files whose scenarios **fail
before any implementation exists**. This is step 1 of the double loop in
[`docs/bdd-strategy.md`](../../../docs/bdd-strategy.md): the failing acceptance scenario is the
"outside" that drives TDD on the "inside". You produce the specification, not the implementation.

**You do not write production code, handlers, step bindings, or drivers.** Your only output is
`.feature` files plus a short coverage map. The [implementer](../implementer/SKILL.md) makes them
green next.

## Inputs

- A WHISPER issue id (e.g. `WHISPER-16`).

## Procedure

1. **Read the issue.** Fetch it from Plane (workspace `dev`, project `WHISPER`). Extract its
   **Acceptance Criteria** and its **Behavior (Gherkin)** block. The issue's own Gherkin is a
   starting sketch — refine it to meet the authoring standards below; never paste it verbatim if it
   violates them.

2. **Identify the capability and behaviors.** Name the capability (a folder under
   `tests/Dictation.Specs/Features/<capability>/`, e.g. `PushToTalk`, `Vad`, `ModelManagement`).
   List the distinct behaviors — **one scenario per behavior**. If an acceptance criterion describes
   two behaviors, it becomes two scenarios.

3. **Map every acceptance criterion to at least one scenario.** Each AC must be covered by a
   scenario; conversely every scenario must trace to an AC. Keep this mapping — you emit it in step 6
   so the [dod-validator](../dod-validator/SKILL.md) can cross-check.

4. **Write the `.feature` file(s)** under `tests/Dictation.Specs/Features/<capability>/`, following
   the authoring standards below. Tag **every** scenario `@WHISPER-<id>`.

5. **Make them start RED — meaningfully, not as compile errors.** A freshly authored feature has no
   step bindings yet, so Reqnroll reports its steps as **undefined/pending** → the scenario fails.
   That is the correct red state. Do **not**:
   - write step bindings or a driver (that is the implementer's job),
   - introduce code that fails to *compile* (a compile error is not a meaningful behavioral failure),
   - tag anything `@wip` to dodge the gate.
   Confirm red by running `dotnet test tests/Dictation.Specs --filter "Category=WHISPER-<id>"` and
   observing the scenarios fail as undefined/pending — not as a build break.

6. **Emit a coverage map** (in your final report, and as a comment block at the top of the feature
   file if helpful) listing each acceptance criterion → the scenario(s) that cover it.

## Gherkin authoring standards (from `docs/bdd-strategy.md` §4)

- **Declarative, not imperative.** Describe *what* behavior is expected, never the clicks/keystrokes.
  `When I dictate "schedule the meeting"`, not `When I press Ctrl+Win for 1200 ms`.
- **One behavior per scenario.** If the title needs "and", split it.
- **Ubiquitous language.** Use the domain's words (`trailing silence`, `push-to-talk release`,
  `CPU fallback`, `TranscriptionResult`) — the same words the Domain/Application types use.
- **No UI/implementation coupling.** No widget names, key codes, API names, file paths, or
  "the SendInput call". Assert on observable behavior or the port boundary (e.g. "text delivered to
  the focused field"), never on internal calls.
- **No incidental detail.** Include only data that affects the outcome. If a silence threshold
  matters, state it; if it doesn't, leave it out.
- **`Scenario Outline` + `Examples`** for the *same* behavior across varying data — not to smush
  different behaviors together.
- **`Background`** only for context truly common to every scenario in the file.
- **Tag with the issue id** (`@WHISPER-<id>`) on every scenario; `@slow` for fixture/IO-heavy ones.

Apply the "definition of a good scenario" checklist from `docs/bdd-strategy.md` §8 before finishing.

## Definition of done (for this skill's output)

- One or more `.feature` files exist under `tests/Dictation.Specs/Features/<capability>/`.
- Every scenario is tagged `@WHISPER-<id>`.
- Every acceptance criterion maps to at least one scenario (coverage map emitted).
- Running the issue's tag shows the scenarios **failing as undefined/pending** (red), with the
  solution still **building** — no compile errors.
- No step bindings, drivers, or production code were written.

## Handoff

Report: the feature file path(s), the AC→scenario coverage map, and confirmation the scenarios are
red. The [implementer](../implementer/SKILL.md) takes it from here.
