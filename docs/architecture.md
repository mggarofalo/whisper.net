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
(WASAPI capture), ONNX Runtime (Silero VAD), SendInput (text injection), SharpHook (global keyboard
hook), persistence, and any opt-in network client. This is the only place real I/O happens.

**Device-seam testing of adapters.** A native adapter is split into device-independent *coordination*
logic and the thin *device glue* that calls the native library. The glue sits behind a small internal
seam (e.g. `IAudioCaptureClient` wraps NAudio's `WasapiCapture`, and `IGlobalKeyHook` wraps SharpHook's
`EventLoopGlobalHook`); the coordination class (e.g. `WasapiAudioSource`, or `EventLoopHotkeyListener`
with its dedicated pump thread and raw-key→domain translation) depends only on that seam. This lets the
adapter's real behavior — idempotent start, flush-on-stop, mapping device errors to typed failures,
clean thread join on dispose — be driven headlessly over a fake seam, while the actual native glue is
verified by manual real-device smoke. Because of this, the BDD specs *do* reference Infrastructure
(since WHISPER-7): they drive the real adapter over a fake low-level seam at the port boundary. Only the
device glue is excluded from automated tests, never the behavior.

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

The host **owns the application lifetime** (WHISPER-12). The WPF `App` has no `StartupUri` and shows
no window: `OnStartup` builds and **starts** the host, so the process runs tray-resident. Long-lived
background components are registered as `IHostedService` — via
`AddAppManagementHostedServices` (wired into `AddWhisperServices`, kept separate so the spec scenario
container is not forced to run a host) — and the Generic Host starts them on launch and stops them on
a **graceful** shutdown (`StopAsync` before the process exits). Unhandled exceptions are logged before
exit. The hotkey listener is the first such hosted component; the host-lifecycle behavior is covered by
the `@WHISPER-12` scenarios driving a real host over the faked hook seam.

Settings persistence is wired into that same lifecycle (WHISPER-43): a `SettingsLifecycleService`
hosted service **loads** the persisted settings into a shared `SettingsHolder` on startup and **saves**
them on graceful shutdown, through the Application-layer `ISettingsStore` port. The file-backed
implementation (`FileSettingsStore`, JSON of the settings DTO) lives in Infrastructure — the only layer
that touches the filesystem — and recovers to defaults (creating the store on a first run, logging on a
corrupt one) so a bad or missing file never crashes the host.

The tray icon (WHISPER-18) follows the same seam discipline: the coordination — mapping the recording
status to the icon/tooltip, and the Open Settings / Quit actions — lives in `Logic.AppManagement`'s
`TrayController`, so it is driven for real in the specs. The thin H.NotifyIcon view and its
CommunityToolkit.Mvvm view-model in Presentation only bind to it. Quit calls
`IHostApplicationLifetime.StopApplication` (the WHISPER-12 graceful path); Open Settings goes through
the `IShellPresenter` port — an Application port **implemented by Presentation** (the WPF shell), the
allowed exception to "ports are implemented by Infrastructure" for UI-surfacing seams.

Single-instance enforcement (WHISPER-25) is the same shape again: the `SingleInstanceCoordinator` in
`Logic.AppManagement` runs before the host starts — it acquires the OS-global lock (`IInstanceLock`) to
become the sole instance, or, if another instance holds it, signals that instance (`IInstanceSignal`)
to surface through `IShellPresenter` and exits without starting a second host. The lock is released
when the host disposes the coordinator on graceful shutdown. The Infrastructure adapters are a named
`Mutex` and a named `EventWaitHandle` in the current-user session namespace (no elevation); both are
composed behind an `OperatingSystem.IsWindows()` guard, like the run-on-login registry adapter.

## Where the rules are enforced

`tests/Architecture.Tests` asserts the dependency rule (Domain depends on nothing; Application does
not reference Infrastructure or Presentation; only Presentation references Infrastructure; etc.).
Adding a forbidden reference turns those tests red — the architecture is executable, not just
documented.
