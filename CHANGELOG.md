# Changelog

All notable changes to this project are documented here. The format is based on
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and the project aims to follow
[Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

## [0.3.0] - 2026-06-30

### Added

- **Start Whisper at login, from the app.** A new **General** settings section adds a "Start Whisper when
  I sign in to Windows" toggle that registers (or removes) the current-user run-key entry. The toggle
  reflects the real registration state on open. This is what makes the app come back after a reboot
  instead of having to be launched by hand — the run-on-login backend shipped in 0.1.0 but had no UI until
  now. (WHISPER-134)
- **Home, History, and Stats update live as you dictate.** The dashboard sections now refresh the moment a
  transcription is recorded — even when they are not the visible tab — instead of going stale until you
  navigate back or click Refresh. (WHISPER-136)

### Fixed

- **Your selected microphone is actually used — and survives a changed device id.** Capture previously
  always opened the OS default device and ignored the saved selection (dictation "worked" only because it
  fell back to the default). The selection is now honored, and because some endpoints (USB / Bluetooth /
  docked mics) get a new device id across reboots, the saved device is recovered by its friendly name when
  the id no longer matches — so the "selected microphone is no longer available" warning stops firing
  spuriously and you do not have to re-pick. The warning now names the device and appears only when it is
  genuinely gone. (WHISPER-135)
- **Model warm-up no longer throws on a shutdown race.** A warm-up that was mid-load when the app shut down
  could release a disposed semaphore, logging an `ObjectDisposedException`. Disposal now cancels and drains
  in-flight work first, and the warm-up unwinds as a clean cancellation. (WHISPER-137)

## [0.2.1] - 2026-06-24

### Security

- **Patched the bundled SQLite (CVE-2025-6965).** Forced the transitive `SQLitePCLRaw` native bundle up
  to the `3.0.x` line (SQLite ≥ 3.50.2), resolving the high-severity memory-corruption advisory
  GHSA-2m69-gcr7-jv3q that the `2.1.11` bundle — pulled transitively by `Microsoft.Data.Sqlite` — shipped.
  (WHISPER-133)

## [0.2.0] - 2026-06-24

### Changed

- **Application and tray icon are now a speech bubble.** The executable, installer, and window use a
  brand-blue speech-bubble icon rendered from the new `assets/whisper.svg` source, and the
  notification-area tray icon is the same glyph tinted by recording state — grey idle, red recording,
  orange transcribing — replacing the previous coloured dot.
- **The app registers as "Whisper" in Windows.** The assembly title and product name are set to
  "Whisper" so the taskbar and notification-area personalization list show "Whisper" instead of
  "Presentation".

## [0.1.0] - 2026-06-13

First public release — whisper.net leaves alpha. A local, GPU-accelerated, tray-resident speech-to-text
dictation utility for Windows: a global hotkey records the microphone, Whisper.net transcribes entirely
on-device, and the text is injected into the focused field. This entry is the complete record of the work
that shipped in 0.1.0, organized by category.

### Added

#### Core dictation pipeline

- **End-to-end dictation orchestrator.** A hotkey press starts microphone capture; releasing finalizes the
  clip and drives it through the delivery pipeline — trim → transcribe → post-process → inject — via an
  explicit, concurrency-guarded state machine (`Idle → Recording → Transcribing → Delivering → Idle`) that
  always returns to a safe idle on error. (WHISPER-14)
- **Push-to-talk and toggle activation** over a single chord pipeline (hold-to-talk or press-to-toggle). (WHISPER-16)
- **Recording state machine with Esc-to-cancel** that drives the tray/UI and aborts an in-progress capture. (WHISPER-22)
- **Continuous dictation mode** — each delivered utterance auto-restarts recording for hands-free dictation;
  Esc exits to idle. (WHISPER-28)
- **Command-mode hook.** Transcripts pass through an `ICommandMatcher` port before delivery so a matched
  phrase can be routed as a command instead of typed (a no-op matcher recognizes nothing by default). (WHISPER-35)

#### Audio capture & voice activity

- **WASAPI microphone capture** behind an `IAudioSource` port. (WHISPER-7)
- **Capture device enumeration, selection, persistence, and hot-swap.** (WHISPER-13)
- **Audio normalization and buffering** of captured audio for transcription. (WHISPER-23)
- **Silero VAD gating and silence trimming** behind an `IVad` port. (WHISPER-31)
- **Bundled Silero VAD ONNX model** shipped with the app and validated by a real-model test. (WHISPER-66)
- **Audio feedback sounds** at recording start, recording stop, and transcription-complete — synthesized
  in-process, fire-and-forget, and configurable on/off. (WHISPER-21)

#### Speech recognition & models

- **Local Whisper.net transcription** behind an `ITranscriber` port — all recognition runs on-device. (WHISPER-3)
- **GPU contact point** that selects an acceleration backend with automatic CPU fallback. (WHISPER-9)
- **Model lifecycle** — warm-up, load/unload/switch, and precision selection. (WHISPER-15)
- **Model registry with opt-in Hugging Face download**, local caching, and progress reporting. The model
  fetch is the only network access here, it is explicit and user-initiated, and no user data is sent. (WHISPER-4)
- **Startup model warm-up** so the first dictation isn't penalized by a cold model load. (WHISPER-127)

#### Text delivery

- **Universal Unicode keystroke delivery** behind an `ITextInjector` port. (WHISPER-2)
- **Clipboard fallback delivery** with change-count-guarded restore. (WHISPER-5)
- **UIPI-blocked elevated-window delivery** surfaced to the user instead of failing silently. (WHISPER-6)
- **Per-delivery text-delivery strategy selection** with override. (WHISPER-8)

#### Hotkeys & activation

- **Global hotkey listener** behind an `IHotkeyListener` port via SharpHook. (WHISPER-10)
- **Hotkey rebinding** via a capture-next-key helper. (WHISPER-30)

#### Post-processing & formatting

- **Transcription normalization** — strip bracketed/parenthesized noise labels (e.g. `[BLANK_AUDIO]`) and
  optionally remove spoken filler words. (WHISPER-36)
- **Custom vocabulary** prompt-token conditioning that biases the decoder toward domain terms. (WHISPER-38)
- **Output-transforms framework** with built-in formats. (WHISPER-37)
- **Configurable post-process pipeline with hot-reload** — edits apply on the next transcription without a
  restart, validated by FluentValidation. (WHISPER-41)
- **Optional AI rephrase (opt-in, localhost-only).** Disabled by default and makes no network call until
  enabled; when enabled it may target only a loopback Ollama endpoint (a remote endpoint is rejected at
  startup), and any backend failure degrades gracefully to the original text. (WHISPER-40)

#### Local data, history & stats

- **SQLite-backed persistence** for the settings and history ports behind a versioned, idempotent migration
  runner (WAL mode); a corrupt database recovers to defaults rather than crashing. (WHISPER-11)
- **Settings persistence across the app lifecycle.** (WHISPER-43)
- **History retention limits and a paged, filterable, most-recent-first browse query.** (WHISPER-17)
- **Usage-statistics recording and aggregation** (totals plus a per-day breakdown). (WHISPER-24)
- **Opt-in audit log with local-only storage and a one-click data purge.** Disabled by default; the audit
  adapter has no network dependency (enforced by an architecture test). (WHISPER-34)
- **Run-on-login toggle** via the current-user registry run key. (WHISPER-32)

#### Desktop app & UI

- **Tray-resident WPF app on the Generic Host.** (WHISPER-12)
- **Tray icon and context menu** via H.NotifyIcon. (WHISPER-18)
- **Single-instance enforcement with activation.** (WHISPER-25)
- **MVVM dashboard shell** with navigation and DI-resolved views. (WHISPER-19)
- **Model picker** with ratings, download progress, and active-model switching. (WHISPER-27)
- **Audio-device and hotkey configuration views.** (WHISPER-33)
- **History browser** with paging and re-copy. (WHISPER-45)
- **Stats dashboard view.** (WHISPER-53)
- **First-run onboarding** with a permissions check. (WHISPER-51)
- **Doctor / selftest diagnostics** (`--doctor`) that runs environment self-checks and exits non-zero on
  failure — what to attach to a bug report. (WHISPER-50)
- **Live recording level overlay** (mini-recorder) showing a smoothed microphone-level meter while recording. (WHISPER-26)
- **Onboarding overhaul** — device dropdown, model picker, and live download progress. (WHISPER-74)
- **Native settings validation** that blocks invalid saves. (WHISPER-77)
- **Instant-apply settings** over a weak-messenger channel. (WHISPER-78)
- **Capture the dictation hotkey by pressing it.** (WHISPER-79)
- **Capture-device combobox** populated from the enumerated devices. (WHISPER-80)
- **Cancelable model downloads** with progress and error reporting. (WHISPER-81)
- **Settings as the single first-run surface.** (WHISPER-82)
- **Keyboard- and screen-reader-accessible settings UI.** (WHISPER-83)
- **User-facing backend-failure notifications** via an `IUserNotifier` port. (WHISPER-95)
- **Auto-load section data on first activation.** (WHISPER-108)
- **Concurrent per-row model downloads.** (WHISPER-107)
- **Compact model table** with one contextual action per row. (WHISPER-105)
- **Live Home status dashboard.** (WHISPER-106)
- **Overlay feedback** — recording state, elapsed time, and a near-cap warning. (WHISPER-102)
- **Live-updating history list** when a transcription is recorded. (WHISPER-114)
- **Light/Dark/System theme switcher**, with the nav sidebar following the active theme and accent. (WHISPER-121, WHISPER-122)
- **Warm-up status** shown in the overlay and on Home, cleared by an app-wide event. (WHISPER-129)

#### Packaging, signing & auto-update

- **Self-contained Velopack installer** — a single-file `win-x64` build with the .NET 10 runtime and native
  assets bundled, built reproducibly from a clean clone with `pwsh ./build/pack.ps1`. (WHISPER-20)
- **Tag-driven release pipeline** — pushing a `vX.Y.Z` tag derives the version via MinVer and builds, tests,
  packages, and publishes the installer and update feed to a GitHub Release; a failing build or test never
  ships. (WHISPER-39)
- **Self-signed signing for personal builds**, plus a build-from-source guide. (WHISPER-72)
- **Opt-in signed auto-update.** Off by default; when enabled the GitHub release feed is the only egress, no
  user data is sent, and a failed or unreachable update is logged and ignored without crashing. (WHISPER-29)

#### Architecture & foundation

- **.NET 10 solution** with central package management and MinVer versioning. (WHISPER-1)
- **Clean-architecture skeleton** with inward-only dependency rules enforced as tests. (WHISPER-54)
- **Source-generated Mediator (CQRS)** with a FluentValidation pipeline. (WHISPER-55)
- **Riok.Mapperly mapping** with house rules and a sample mapping. (WHISPER-56)
- **Generic Host composition** with Serilog and per-layer DI. (WHISPER-57)
- **Domain model** — entities, value objects, and invariants. (WHISPER-42)
- **Infrastructure-facing ports** defined in the Application layer. (WHISPER-44)
- **Settings and history CQRS** (query/command via Mediator) plus **usage-stats aggregation**. (WHISPER-46, WHISPER-47, WHISPER-48)
- **Reqnroll BDD harness** proving the outside-in double loop. (WHISPER-58)
- **GitHub Actions CI** — build, format, test, coverage, vulnerability scan, and PR-title check. (WHISPER-59)
- **Canonical repository guidance** and the commitlint commit-msg hook. (WHISPER-60)
- **Agent skills** — spec-author, implementer, DoD-validator, and Plane-lifecycle. (WHISPER-61, WHISPER-62, WHISPER-63, WHISPER-64)

### Changed

- **MVVM hardening.** Introduced an `IUiDispatcher` threading seam, codified view resolution (removing
  residual code-behind), sanctioned the background-thread collection-update pattern, adopted XAML behaviors
  for declarative event wiring, and formalized the view-model activation lifecycle with messenger-leak
  discipline. (WHISPER-90, WHISPER-91, WHISPER-92, WHISPER-93, WHISPER-94)
- **Settings and feature view-models** rebased on `ObservableValidator`. (WHISPER-76)
- **Built-in WPF Fluent theme** applied across the UI. (WHISPER-84)
- **STA view smoke harness and template-completeness gate** added to the test suite. (WHISPER-96)
- **Presentation smoke run** no longer crashes on an unresolved `ISettingsStore`. (WHISPER-128)
- **M1 Mapperly mapper set** completed and verified. (WHISPER-49)
- **Vulkan feasibility spike** validating Whisper.net on the RTX 5080. (WHISPER-65)
- **SharpHook global-hotkey adapter** documented (M5).
- **Commit hook** now invokes commitlint directly instead of via `npx`.
- **Corrected a hotkey scenario's traceability tag.** (WHISPER-75)

### Fixed

- **Installed-app file logging and crash-safe startup.** (WHISPER-73)
- **Transcription works in the installer** — the Whisper native runtime is bundled as loose files. (WHISPER-85)
- **Dictation produces text** — the transcriber loads the active model. (WHISPER-87)
- **Keystrokes deliver** — the SendInput `INPUT` union is sized correctly. (WHISPER-88)
- **No installer/update collision** — per-user data moved out of the Velopack install directory. (WHISPER-86)
- **Tab state survives navigation** — feature view-models are cached per shell rather than recreated. (WHISPER-89)
- **Selecting a model actually changes the model dictation uses** — the active-model selection is persisted
  and the in-memory settings holder stays in sync, so shutdown no longer reverts it. (WHISPER-98)
- **The assigned hotkey takes effect** — it is applied to the live matcher at startup and on change, and
  settings are loaded on hotkey-section activation. (WHISPER-76, WHISPER-109)
- **Delivered transcriptions are recorded to history**, with load-more state exposed. (WHISPER-110)
- **Quiet trailing words survive** — the capture tail is drained with a post-release grace window and
  end-of-speech is detected by energy rather than a hard cutoff. (WHISPER-112)
- **Long dictation isn't truncated** — the 30-second hard cap is replaced with a growable buffer, soft-limit
  signals, and a stop-and-transcribe failsafe. (WHISPER-111)
- **Silent clips are skipped** so Whisper can't hallucinate text on them. (WHISPER-125)
- **An in-flight recording is canceled** when the hotkey is reconfigured. (WHISPER-126)
- **The first recording is no longer mis-sampled** — the capture format is read after Start. (WHISPER-132)
- **Warm-up is not lost** — it is serialized against the first dictation, and the warm-up pill shows because
  the overlay subscribes before the host starts. (WHISPER-130, WHISPER-131)
- **The model list scrolls** — it is wrapped in a `ScrollViewer`, and scrollbar room is reserved so it
  doesn't cover the row buttons. (WHISPER-104, WHISPER-120)
- **Dictation overlay placement** — anchored to the bottom-center of the work area, kept on-screen on scaled
  displays, and no longer clipped at its right edge. (WHISPER-100, WHISPER-117, WHISPER-124)
- **The overlay meter** is mapped to a perceptual dBFS scale. (WHISPER-101)
- **Nav sidebar contrast** — restyled for the dark theme at WCAG AA, with labels following the theme rather
  than system black. (WHISPER-103, WHISPER-123)
- **Local-time correctness** — history timestamps render in local time and usage stats bucket by the user's
  local day. (WHISPER-115, WHISPER-116)
- **The persisted active model is shown in the picker** before it loads. (WHISPER-118)
- **The Home dashboard refreshes** on every activation. (WHISPER-119)

[Unreleased]: https://github.com/mggarofalo/whisper.net/compare/v0.3.0...HEAD
[0.3.0]: https://github.com/mggarofalo/whisper.net/releases/tag/v0.3.0
[0.2.1]: https://github.com/mggarofalo/whisper.net/releases/tag/v0.2.1
[0.2.0]: https://github.com/mggarofalo/whisper.net/releases/tag/v0.2.0
[0.1.0]: https://github.com/mggarofalo/whisper.net/releases/tag/v0.1.0
