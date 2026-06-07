# Architecture

`whisper.net` follows **Clean Architecture** with **CQRS**. This document describes the layers, the
dependency rules that bind them, and how a request flows through the system. The rules here are not
aspirational — they are enforced as tests in `tests/Architecture.Tests` (NetArchTest), so a violation
fails the build.

## Layers and the dependency rule

Dependencies point **inward only**. An outer layer may reference inner layers; an inner layer must
never reference an outer one.

```
┌─────────────────────────────────────────────────────────────┐
│ Presentation (WPF + MVVM)        ← the only layer that        │
│                                    references Infrastructure   │
│  ┌───────────────────────────────────────────────────────┐  │
│  │ Infrastructure   implements the Application ports        │  │
│  │  ┌─────────────────────────────────────────────────┐  │  │
│  │  │ Logic.*   deterministic behavior, no I/O          │  │  │
│  │  │  ┌───────────────────────────────────────────┐  │  │  │
│  │  │  │ Application   handlers · ports · DTOs ·     │  │  │  │
│  │  │  │               validators · mappers          │  │  │  │
│  │  │  │  ┌─────────────────────────────────────┐  │  │  │  │
│  │  │  │  │ Domain   entities · value objects    │  │  │  │  │
│  │  │  │  │          (no dependencies)           │  │  │  │  │
│  │  │  │  └─────────────────────────────────────┘  │  │  │  │
│  │  │  └───────────────────────────────────────────┘  │  │  │
│  │  └─────────────────────────────────────────────────┘  │  │
│  └───────────────────────────────────────────────────────┘  │
└─────────────────────────────────────────────────────────────┘
```

### Domain
Entities and value objects expressing the dictation domain (audio clips, transcription results,
durations, thresholds). **No dependencies** — not on Application, not on any framework. Pure C#.

### Application
The orchestration and contract layer:

- **Handlers** — CQRS command/query handlers that orchestrate behavior. Handlers contain
  *orchestration*, not business math (that lives in `Logic.*`) and not I/O (that lives behind ports).
- **Ports** — the interfaces Infrastructure implements (`ITranscriber`, `IAudioSource`,
  `ITextInjector`, …). Ports live **here**, in Application, so inner layers and specs depend only on
  the abstraction.
- **Logic abstractions** — the interfaces `Logic.*` implements are also declared in Application (or
  Domain). This is the key inversion: **Application depends only on Domain**, never on `Logic.*`.
  Handlers reference a `Logic.*` behavior through its Application-declared interface; the concrete
  `Logic.*` type is supplied by DI. Hence the dependency arrow points *into* Application
  (`Logic.* → Application`), not out of it.
- **DTOs**, **FluentValidation** validators, and **Mapperly** mappers.

### Logic.\*
Deterministic, side-effect-free behavior split by concern:

- `Logic.AppManagement` — app-level coordination policy.
- `Logic.AudioManagement` — silence trimming, resampling, buffering policy.
- `Logic.ModelManagement` — model lifecycle/selection policy.
- `Logic.GpuContactPoint` — **the single GPU touch point** (Vulkan detect / CPU fallback policy).

`Logic.*` is real in every test — it is **never faked**. Faking it would mean the test exercises
nothing.

### Infrastructure
Implements the Application ports against the outside world: Whisper.net (transcription), NAudio
(WASAPI capture), ONNX Runtime (Silero VAD), SendInput (text injection), persistence, and any
opt-in network client. This is the only place real I/O happens.

### Presentation
WPF + MVVM (the tray app, settings, overlays). It is the **only** layer permitted to reference
Infrastructure, where it composes the object graph at startup. The WPF project targets
`net10.0-windows`, which is why CI runs on `windows-latest`.

## CQRS via source-generated Mediator

All application requests flow through the **source-generated Mediator** (martinothamar,
`Mediator.Abstractions` + `Mediator.SourceGenerator`) — **not** MediatR. We use custom marker
interfaces, `ICommand<T>` and `IQuery<T>`, so commands (state-changing) and queries (read-only) are
distinguishable at the type level.

A request flows: **Presentation/spec** sends an `ICommand<T>`/`IQuery<T>` → `IMediator` → pipeline
behaviors (e.g. `ValidationBehavior` running FluentValidation) → the handler → `Logic.*` + ports.
Cross-cutting concerns (validation, logging) are pipeline behaviors, not handler code.

## Composition

The app is composed on the **Generic Host** with Microsoft DI and Serilog. Each layer exposes a
single `AddX(IServiceCollection, IConfiguration?)` registration extension
(`AddApplication`, `AddAppManagement`, `AddAudioManagement`, `AddModelManagement`,
`AddGpuContactPoint`, and Infrastructure's `AddWhisperServices` for the production composition).

Critically, the **BDD specs reuse the same inner `AddX` extensions** and substitute only the
Infrastructure ports — so scenarios exercise production composition (real behaviors, real pipeline,
real mapping) rather than a parallel wiring that could drift. See
[`docs/bdd-strategy.md`](bdd-strategy.md) §2.

## Where the rules are enforced

`tests/Architecture.Tests` asserts the dependency rule (Domain depends on nothing; Application does
not reference Infrastructure or Presentation; only Presentation references Infrastructure; etc.).
Adding a forbidden reference turns those tests red — the architecture is executable, not just
documented.
