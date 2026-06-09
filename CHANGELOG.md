# Changelog

All notable changes to this project are documented here. The format is based on
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and the project aims to follow
[Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Fixed

- **App data no longer collides with the Velopack install directory.** Per-user data (model cache, logs,
  settings database) moved out of `%LOCALAPPDATA%\whisper.net`, which on case-insensitive Windows was the
  Velopack install root (`%LOCALAPPDATA%\Whisper.Net`, the PackId). The collision made the installer fail
  with "Failed to remove existing application directory" and blocked updates while the app ran (its open
  rolling-log handle was locked inside the install dir). Data now lives under `whisper-net` (logs +
  model cache under `%LOCALAPPDATA%\whisper-net\`, the settings DB under `%APPDATA%\whisper-net\`), a name
  that can never equal the PackId — pinned by a test. Existing local data is not migrated (a dev machine
  re-downloads its model on next use); the locations are documented in
  [`docs/packaging.md`](docs/packaging.md). (WHISPER-86)

### Added

- **Signed auto-update (opt-in).** The app can update itself from its release channel via Velopack: it
  checks GitHub Releases for a newer version, downloads it, and stages it to apply on the next restart.
  This is **outbound network access** and is therefore **off by default** — nothing is checked or
  downloaded unless you enable it (`AutoUpdate` settings); when enabled the release feed
  (`https://github.com/mggarofalo/whisper.net`) is the **only** egress and no user data is sent. A failed
  or unreachable update is logged via Serilog and ignored — the app keeps running on the current version,
  never crashing. The update policy (`AutoUpdateService`) is WPF/Velopack-free and unit/BDD-tested over a
  faked update source; the installer and app are Authenticode code-signed in the release pipeline when a
  signing certificate is supplied from a secret (never committed). (WHISPER-29)

- **Self-contained Velopack installer.** The WPF tray app is packaged as a self-contained, single-file
  `win-x64` build (the .NET 10 runtime and native assets bundled) wrapped in a Velopack installer, built
  reproducibly from a clean clone with `pwsh ./build/pack.ps1`. The version is derived from git tags by
  MinVer (never hand-edited). A tag-driven GitHub Actions workflow builds, tests, packages, and publishes
  the installer + update package to a GitHub Release on a `vX.Y.Z` tag. (WHISPER-20, WHISPER-39)

- **Doctor / selftest diagnostics.** A `--doctor` launch runs environment self-checks (audio capture,
  model cache, hotkey registration, GPU/Vulkan) through the existing ports and prints a clear
  pass/warn/fail report, exiting non-zero when any check fails — what to attach to a bug report. (WHISPER-50)

- **Live recording level overlay.** A small, frameless, top-most, click-through-tolerant WPF overlay
  (mini-recorder) appears while recording and shows a live, smoothed microphone-level meter, then hides
  when recording stops. Its coordination logic lives in a WPF-free, unit-testable `LevelOverlayController`
  (observes the recording state the orchestrator drives and turns captured frames into a 0–1 level); a
  thin CommunityToolkit.Mvvm view-model and code-built window bind to it. The level math only reads the
  frames capture already raises, so it never blocks or stalls the dictation pipeline. (WHISPER-26)
- **Audio feedback sounds.** The pipeline can play a short, distinct sound at each dictation event —
  recording start, recording stop, and transcription complete — behind a new `IAudioFeedback` port. The
  orchestrator fires the cue at each transition; playback is fire-and-forget and never blocks or fails
  dictation (a playback failure is logged and swallowed). Feedback is configurable on/off (`AudioFeedback`
  section, on by default); when disabled, no sound plays and no playback resource is allocated. The
  Infrastructure player synthesizes the tones in-process, so they ship with the app and need no on-disk
  asset path. (WHISPER-21)
- **Continuous dictation mode.** The orchestrator gained a continuous mode: once entered, each delivered
  utterance auto-restarts recording for the next one instead of returning to rest, so a long passage can
  be dictated hands-free. Pressing **Esc** exits the mode and returns the pipeline to idle without
  restarting (any in-flight capture is discarded). With the mode off the pipeline stays single-shot
  (one capture → deliver → idle); each entry, exit, and auto-restart is logged, and the loop is bounded
  (a restart waits in `Recording` for the next stop, so it cannot spin). (WHISPER-28)
- **Command-mode hook (scaffolding).** The delivery pipeline now consults an `ICommandMatcher` port
  after transcription/clean-up and before text delivery: a matched transcript is routed to the command
  branch (reported as `DeliveryResult.MatchedCommand`) instead of being typed, while unmatched speech
  falls through to normal delivery. The default `NoOpCommandMatcher` recognizes nothing, so dictation is
  unchanged until a real matcher exists. This is the hook + abstraction only — no command catalogue or
  execution engine is built here. (WHISPER-35)
- **End-to-end dictation orchestrator.** A new `DictationOrchestrator` (`Logic.AppManagement`) is the
  coordination hub that runs one utterance end to end: a hotkey press begins microphone capture through
  the `IAudioSource` port, a release/stop finalizes the captured audio into a clip and drives it through
  the existing delivery pipeline (`DeliverTranscriptionCommand` via Mediator) — trim → transcribe →
  post-process → inject — with no manual step in between. It owns an explicit pipeline state machine
  (`Idle → Recording → Transcribing → Delivering → Idle`) guarded against concurrent transitions, keeps
  the shared `RecordingStateMachine` in step for the tray/UI, and logs every transition and stage duration
  structurally. Any stage error (a failed transcription/delivery or a capture-device failure) is logged
  and returns the pipeline to a safe `Idle` so it can never get stuck. The host activates it for the app
  lifetime and bridges the global hotkey listener into the activation controller, closing the
  hotkey → capture → transcribe → deliver path. (WHISPER-14)
- **Privacy-gated audit log + data purge.** Transcript history is stored locally as before; a separate,
  more verbose **audit log** is **disabled by default** and is written only after an explicit opt-in
  (`AuditLogEnabled`). The gate (`AuditLogger`) reads the live settings, so enabling/disabling it takes
  effect without a restart. The audit log is **local-only** — its SQLite adapter has no network dependency
  (enforced by an architecture test) — and a user-initiated `PurgeUserDataCommand` clears both the
  transcript history and the audit log from disk. (WHISPER-34)
- **Usage statistics recording and aggregation.** Each transcription now records its captured audio
  duration (persisted via a schema migration); `GetUsageSummaryQuery` aggregates history into totals
  (transcriptions, characters, audio duration) plus a per-day breakdown, so the measures survive a restart
  and a recording failure never blocks the pipeline. (WHISPER-24)
- **History retention + paged browse.** History is capped at a configurable limit (`History:MaxEntries`,
  default 1000) by pruning the oldest entries after each write, and is read back through a paged,
  most-recent-first `BrowseHistoryQuery` with optional text/date filtering and validated paging. (WHISPER-17)
- **SQLite persistence.** A single local SQLite database backs the settings and history ports behind a
  versioned, idempotent migration runner (WAL mode), replacing the JSON settings file; a corrupt database
  recovers to defaults rather than crashing the host. (WHISPER-11)
- **Post-process pipeline configuration + hot-reload.** A single `PostProcess` configuration section
  exposes filler removal on/off, the custom vocabulary, the default output transform, and rephrase
  enable + endpoint. The ordered pipeline (normalize → optional transform; vocabulary-conditioned decode
  is applied upstream during transcription) runs behind the `IPostProcessor` port and reads the live
  configuration, so an edit applied via the `ConfigurePostProcessing` command takes effect on the next
  transcription without restarting the app. The configuration is validated by FluentValidation through
  the existing `ValidationBehavior` pipeline (unknown default transform / non-loopback rephrase endpoint
  rejected), and an unknown transform degrades safely to the normalized text. (WHISPER-41)
- **Optional AI rephrase (opt-in, localhost-only).** An optional post-processing step can rewrite
  recognized text with a locally-hosted [Ollama](https://ollama.com) model via the `IRephraseClient`
  port (`OllamaRephraseClient`). Privacy stance: it is **disabled by default** and makes **no network
  call** until explicitly enabled; when enabled it may only target a **loopback** endpoint
  (`localhost`/`127.0.0.1`/`[::1]`) — a remote endpoint is rejected at startup rather than silently
  used. Backend failures (Ollama down, timeout, non-2xx) degrade gracefully to the original text and
  never crash the dictation pipeline. This is the single disclosed transcript-bearing network seam.
  (WHISPER-40)
- **Custom vocabulary prompt-token conditioning.** A user-supplied vocabulary biases the Whisper
  decoder toward domain terms via an initial prompt, disabling the first-token log-probability
  threshold so the injected prompt cannot drop the genuine first token. Changes apply on the next
  transcription without reloading the model. (WHISPER-38)
- **Transcription normalization.** Bracketed/parenthesized noise labels (e.g. `[BLANK_AUDIO]`) are
  always stripped, and spoken filler words are removed when the "remove filler words" setting is on.
  (WHISPER-36)
