# AGENTS.md — canonical guidance for whisper.net

> **This file is the single source of truth** for how humans and AI agents work in this
> repository. `CLAUDE.md` is a one-line redirect here; everything authoritative lives in this
> file and the linked documents under `docs/`. If guidance ever conflicts, this file wins.

`whisper.net` is a local, GPU-accelerated speech-to-text dictation utility for Windows — a
ground-up **.NET 10 WPF** rewrite of the Python `whisper-local` app. It is tray-resident: a global
hotkey records the microphone, Whisper.net transcribes locally, and the text is injected into the
focused field.

## Build, run, test

```bash
dotnet restore Whisper.slnx
dotnet build   Whisper.slnx -p:TreatWarningsAsErrors=true     # build clean (warnings are errors)
dotnet format  Whisper.slnx --verify-no-changes               # formatting gate (.editorconfig)
dotnet test    Whisper.slnx --filter "Category!=wip&Category!=slow"   # unit + Reqnroll specs (fast gate)
dotnet test    Whisper.slnx --filter "Category=slow"                  # @slow real-model tests (opt-in)
```

`dotnet test` runs the xUnit unit projects and the Reqnroll acceptance project (`tests/Dictation.Specs`)
together. The fast gate excludes `@wip` (unfinished scenarios) and `@slow` (tests that load the real
Silero VAD model and run ONNX inference); CI runs the `@slow` tests in their own step. The same commands
are what CI runs (`.github/workflows/ci.yml`, `windows-latest` — WPF requires Windows).

First-time setup also installs the Git commit hooks (see [Commit conventions](#commit-conventions)):

```bash
npm install        # wires the commitlint commit-msg hook via husky
```

## Architecture (summary)

Clean Architecture + CQRS. Dependencies point inward only:

```
Domain ← Application ← Logic.* ← Infrastructure ← Presentation (WPF)
```

- **Domain** — entities, value objects; no dependencies.
- **Application** — Mediator handlers, **ports** (the interfaces Infrastructure implements), DTOs,
  FluentValidation validators, Mapperly mappers.
- **Logic.\*** — `Logic.AppManagement`, `Logic.AudioManagement`, `Logic.ModelManagement`,
  `Logic.GpuContactPoint` (the single GPU touch point). Real, deterministic behavior — never faked
  in tests.
- **Infrastructure** — implements the Application ports (Whisper.net, NAudio, ONNX VAD, SendInput,
  persistence). The only project that does real I/O.
- **Presentation** — WPF + MVVM. The only project that references Infrastructure.

CQRS flows through the source-generated **Mediator** (martinothamar) — **not** MediatR — using custom
`ICommand<T>` / `IQuery<T>` markers. Architectural rules are enforced as tests in
`tests/Architecture.Tests`. Full detail: [`docs/architecture.md`](docs/architecture.md).

## How we test (BDD + TDD)

Behavior is governed by Gherkin. We run an outside-in **double loop**: a failing Reqnroll
`.feature` scenario (outer, behavior) drives xUnit red-green-refactor (inner, implementation) until
the scenario goes green. Scenarios drive **real** Application + Logic composition through `IMediator`;
only Infrastructure ports are substituted.

- Every scenario is tagged with its Plane issue id (`@WHISPER-<id>`) for traceability.
- An issue is **Done** only when every acceptance criterion maps to a green `@WHISPER-<id>` scenario,
  supporting unit tests are green, and coverage is sane (~80% **guideline, not a gate**).
- Prefer **no test** over a box-checking test.

The complete, opinionated strategy — Reqnroll setup, the Driver pattern, Gherkin authoring standards,
what to BDD vs not, and the Definition of Done — is in [`docs/bdd-strategy.md`](docs/bdd-strategy.md).

## Coding standards

House rules for Mediator (CQRS), Mapperly (mapping), and FluentValidation (validation) live in
[`docs/coding-standards.md`](docs/coding-standards.md). Formatting is enforced by `.editorconfig` +
`dotnet format` and is not restated there.

## Plane workflow

Work is tracked in the **WHISPER** project (workspace `dev`). Branch naming, the
Backlog → In Progress → PR → Done lifecycle, conventional-commit PRs, and `@WHISPER-<id>` scenario
tagging are documented in [`docs/plane.md`](docs/plane.md). Never leave an issue in an ambiguous
state: an issue moves to **Done** only after its PR is squash-merged to `main` and its acceptance
criteria are validated.

## Commit conventions

Commits and PR titles follow [Conventional Commits](https://www.conventionalcommits.org/). The
allowed types and scopes are defined once in `commitlint.config.mjs` and mirrored by the CI PR-title
check.

- **Types:** `feat`, `fix`, `docs`, `refactor`, `test`, `chore`.
- **Scopes (optional):** `domain`, `application`, `logic`, `infrastructure`/`infra`, `presentation`,
  `ci`, `docs`, `tests`, `hooks`, `build`.

A **commitlint commit-msg hook** rejects non-conventional messages locally. It is installed by
`npm install` (husky wires `core.hooksPath` to `.husky/`). The hook prefers the installed commitlint
CLI and falls back to a built-in Conventional Commits check when Node dependencies are not present,
so commits are guarded either way.

## Privacy stance

**Privacy is non-negotiable.** No feature may send audio, transcripts, or any user data off the
device without an **explicit, opt-in** user action. Transcription runs entirely locally (Whisper.net).
Any future network capability (e.g. model download, an opt-in localhost rephrase) must be disabled by
default, gated behind a clear user prompt, and disclosed in `README.md` and the changelog. There is no
telemetry.

## The agents (M0)

This repo is built under a fully AI-driven model. Four Claude skills under `.claude/skills/`
encode the workflow so agents apply it consistently:

- **spec-author** — turns a WHISPER issue into failing, tagged Gherkin scenarios (outer-loop start).
- **implementer** — drives the inner TDD red-green-refactor loop to green.
- **dod-validator** — gates the Done transition (AC → scenario mapping, green scenarios, coverage).
- **plane-lifecycle** — performs the Plane state transitions (branch → In Progress → PR → Done).

Each skill's `SKILL.md` is self-describing; this section is just the map.
