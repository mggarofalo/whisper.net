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

The **view-models are WPF-free and live in `Logic.AppManagement`** (e.g. the dashboard `ShellViewModel`
and its feature view-models, the `TrayController`, the `LevelOverlayController`), built on
`CommunityToolkit.Mvvm` — a UI-framework-agnostic library with no WPF dependency. They depend only on
`IMediator` (and Logic/Domain types), never on ports or Infrastructure. The WPF project holds only the
thin views that bind to them, so the MVVM behavior (navigation, commands dispatching through Mediator)
is driven for real in the Reqnroll specs while the views are verified by manual smoke. The dashboard
shell (`WHISPER-19`) resolves each section's view-model from the DI container through an
`INavigationService`, so feature views plug in without the shell knowing them.

### View ↔ view-model resolution (WHISPER-92)

This is the codified standard for how a view meets its view-model; deviations are spec-enforced by
the `@WHISPER-92` scenarios.

- **Views are resolved by implicit `DataTemplate`s keyed on the view-model type.** The shell window's
  resources map each feature view-model (`HomeViewModel`, `ModelViewModel`, …) to its view; the
  content region is a `ContentControl` bound to `CurrentViewModel`. Adding a section means adding a
  view-model, a view, a `NavigationSection` registration, and one `DataTemplate` — nothing else.
- **View-models are supplied by the DI container, never located.** The `NavigationService` resolves
  each section's view-model from the shell's UI scope. There is **no `ViewModelLocator`**, no service
  lookup from a view, and no per-view `DataContext` assignment in code-behind. The one composition-root
  exception is the shell window itself, which receives its injected `ShellViewModel` at construction.
- **Feature-view code-behind is `InitializeComponent`-only.** A view reacts to view-model state through
  data bindings (or a declarative behavior), never by subscribing to `PropertyChanged` and switching on
  property names — a renamed property must surface as a binding/compile break, not a silent no-op.
  Decisions (like the device picker's commit-on-genuine-user-pick) belong in the view-model, where the
  specs drive them.
- **Legitimate code-behind is the narrow exception**: a self-contained, reusable input control that
  adapts raw UI events into a bindable `DependencyProperty` contract (e.g. `HotkeyCaptureControl`'s
  keyboard capture). Such a control owns no application behavior and exposes everything testable
  through its bound properties.

### Background-thread collection updates (WHISPER-91)

WPF throws a cross-thread exception when a bound `ObservableCollection` is mutated off the UI thread
unless the collection was registered for synchronization. The sanctioned pattern, which new
list-bearing view-models adopt by default:

- **Expose a `UiBoundCollection<T>`** (Logic.AppManagement.Shell) instead of a raw
  `ObservableCollection<T>`. Every mutation automatically takes the collection's `Gate`, so callers
  cannot forget the lock.
- **Register it at construction** through the `IUiCollectionSynchronizer` port:
  `synchronizer.Enable(Entries)`. Construction happens before the view can bind, and the WPF
  implementation (`WpfCollectionSynchronizer`) calls `BindingOperations.EnableCollectionSynchronization`
  on the UI thread via the `IUiDispatcher` fast-path, so the binding engine reads the collection under
  the same gate the mutations take.
- Specs substitute a recording synchronizer, keeping the view-models WPF-free; the `@WHISPER-91`
  scenarios pin registration-before-binding, locked mutation, and background-thread loading.

`HistoryViewModel.Entries` is the reference implementation of the pattern.

### Event wiring: behavior vs command vs legitimate code-behind (WHISPER-93)

How a view connects a UI event to logic, in order of preference:

1. **A real `Command` binding** for every user intent where enablement matters (buttons, menu items):
   `Command="{Binding AssignCommand}"`. The control's enabled state follows `CanExecute` for free.
   **Caveat:** `InvokeCommandAction` does **not** honor `CanExecute` — it invokes the command even when
   `CanExecute` is false and never disables the control. Where enablement matters, use a real command
   binding, not a trigger.
2. **A named, reusable attached behavior** (`Presentation/Behaviors`, built on
   `Microsoft.Xaml.Behaviors.Wpf`'s `Behavior<T>`) for view-side reactions to lifecycle/UI events —
   e.g. `FocusOnActivateBehavior` focuses a view's primary control each time its section is shown.
   The event subscription lives once, inside the behavior's attach/detach lifecycle, never in a view's
   code-behind. Use `Interaction.Triggers` + `InvokeCommandAction` only for fire-and-forget
   notifications to the view-model where enablement is irrelevant.
3. **Legitimate code-behind** stays the narrow WHISPER-92 exception: a self-contained input control
   adapting raw UI events into a bindable `DependencyProperty` contract (`HotkeyCaptureControl`).
   Everything else in a view is markup.

### View-model activation lifecycle (WHISPER-94)

Feature view-models are **cached per shell UI scope** (WHISPER-89), so navigation toggles activation
instead of recreating. The lifecycle rule:

- Every feature view-model derives from `FeatureViewModel`, whose `OnNavigatedTo`/`OnNavigatedFrom`
  flip `IsActive` exactly once per transition and call the `OnActivated`/`OnDeactivated` hooks.
- **Live subscriptions belong in the hooks**: register messenger/controller subscriptions in
  `OnActivated`, remove them in `OnDeactivated`. An inactive cached view-model holds no live
  subscriptions and gets no callbacks — it is dormant state, not a background listener.
- **Cached view-models are deactivated on navigate-away and disposed only at shell teardown**, when
  the UI scope that owns them is disposed. Navigation never disposes a section.
- **Data sections auto-load on FIRST activation** (WHISPER-108): a section that shows queried data
  (Model, Audio, History, Stats) overrides `FirstActivationLoadCommand` with its load command, and
  the base executes it once — after `OnActivated`, so subscriptions are live before the first load
  runs. The once-per-instance guard keeps a cached instance from re-querying on every tab switch;
  Refresh stays the explicit manual re-query, and the views show a lightweight "Loading…" state bound
  to the command's `IsRunning`. A section that must re-sync on EVERY activation (Hotkey, WHISPER-109)
  keeps its own `OnActivated` trigger and leaves the hook null.
- The messenger standard is CommunityToolkit's **`WeakReferenceMessenger`** (registered once by
  `AddApplication`), so even a missed unregister can degrade freshness but can never root a cached
  view-model — leaks are impossible by construction, and the activate/deactivate discipline is about
  correctness (no stale callbacks), not memory.

`HotkeyViewModel` is the reference recipient: it registers for `SettingsChangedMessage` on activate
(so an instant-apply commit refreshes its displayed binding) and unregisters on deactivate — the
contract M12 live-apply recipients follow.

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
them on graceful shutdown, through the Application-layer `ISettingsStore` port.

Persistence is backed by a single local **SQLite** database (WHISPER-11). Both the `ISettingsStore` and
`IHistoryStore` ports are implemented over it in Infrastructure — the only layer that touches storage; no
Application or Logic code references SQLite. The schema is versioned with SQLite's `user_version` PRAGMA
and brought forward by an ordered, idempotent `SqliteMigrationRunner` on first use (connections run in WAL
mode); the database file defaults to a per-user application-data path. Settings are stored as the JSON of
the settings DTO in a single-row table, and the store recovers to defaults (creating the schema on a first
run, logging on a corrupt database) so a bad or missing file never crashes the host.

History stays bounded by a retention policy (WHISPER-17): after each new transcription is recorded, the
`RecordTranscriptionCommand` handler prunes entries beyond the configured `History:MaxEntries` cap
(default **1000**; a non-positive value disables pruning). History is read back through a paged
`BrowseHistoryQuery` — most-recent-first, with optional case-insensitive text and date-range filtering —
whose paging inputs are validated (`BrowseHistoryQueryValidator`) before the handler runs.

Each recorded transcription also carries the captured audio duration (WHISPER-24), persisted alongside
the text and word count. `GetUsageSummaryQuery` aggregates history into a `UsageSummary` — total
transcriptions, characters, and audio duration, plus a most-recent-first per-day breakdown — computed by
the Logic `UsageStatsCalculator` and Mapperly-projected to a DTO. Because the measures live in the store,
the totals survive a restart; a recording failure is logged and swallowed by the store, so it never blocks
the transcription pipeline.

Transcript text is kept in history by design, but a verbose **audit log** is privacy-sensitive and is
**off by default** (WHISPER-34). The gate is the Logic `AuditLogger`, which reads the live settings holder
on every call, so `AuditLogEnabled` can be toggled without a restart; only when it is on does a record
reach the local `IAuditLog` (`SqliteAuditLog`, a separate table). The audit log is **local-only** — its
adapter has no network dependency (enforced by an architecture test) — and a user-initiated
`PurgeUserDataCommand` clears both the transcript history and the audit log from disk.

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
