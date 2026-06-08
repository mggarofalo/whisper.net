# whisper.net

A local, GPU-accelerated speech-to-text **dictation utility for Windows**, built on .NET 10 + WPF.
It is tray-resident: a global hotkey records the microphone, [Whisper.net](https://github.com/sandrohanea/whisper.net)
transcribes **entirely on your machine**, and the recognized text is injected into whatever field has
focus. It is a ground-up rewrite of the Python [`whisper-local`](https://github.com/drajb/whisper-local) app.

> **Privacy:** transcription runs locally. No audio, transcripts, or user data ever leave the device
> without an explicit, opt-in action. There is no telemetry. See
> [AGENTS.md → Privacy stance](AGENTS.md#privacy-stance).
>
> **Network disclosure:** the only model-related network access is downloading a Whisper model you
> choose, on request, from Hugging Face (`https://huggingface.co/ggerganov/whisper.cpp`). Models are
> never fetched automatically; once cached locally they are reused with no further network access, and
> transcription itself is always fully offline.
>
> **Optional AI rephrase (opt-in, localhost-only):** you may enable an optional step that rewrites the
> recognized text with a locally-hosted [Ollama](https://ollama.com) model. It is **disabled by
> default** and makes **no network call** unless you turn it on. When enabled it may only target a
> **loopback** endpoint (`localhost`/`127.0.0.1`/`[::1]`); a remote endpoint is rejected at startup, so
> transcript text never leaves your machine. If the local model is unavailable the original text is kept
> unchanged. This is the only transcript-bearing network seam in the app.
>
> **Automatic updates (opt-in, off by default):** the app can check for a newer **signed** version and
> update itself via [Velopack](https://velopack.io). This is **disabled by default** and makes **no
> network call** unless you enable it (`AutoUpdate` settings). When enabled, the **only** egress is the
> release feed — the project's GitHub Releases
> (`https://github.com/mggarofalo/whisper.net`) — and **no user data is ever sent**; it only fetches the
> update. A failed or unreachable update is logged and ignored, and the app keeps running on the current
> version.
>
> **History &amp; audit log (local-only):** your transcription history is stored in a local SQLite
> database. A more verbose **audit log** is **off by default** and is written only after you explicitly
> enable it; it never leaves the device. You can clear both your history and the audit log from disk at
> any time with a one-click purge.

## Build and run

```bash
dotnet restore Whisper.slnx
dotnet build   Whisper.slnx -p:TreatWarningsAsErrors=true
dotnet test    Whisper.slnx --filter "Category!=wip"
npm install    # one-time: installs the commitlint commit-msg hook
```

## Packaging

The app ships as a self-contained, single-file Windows build wrapped in a [Velopack](https://velopack.io)
installer — no separate .NET install required. Build it from a clean clone with one command:

```pwsh
pwsh ./build/pack.ps1     # -> ./releases/Whisper.Net-win-Setup.exe (+ update package & feed)
```

The version is derived from git tags by MinVer (never hand-edited). Full details, including the
install/update hooks and release flow, are in **[docs/packaging.md](docs/packaging.md)**.

## How we test

Behavior is governed by Gherkin under an outside-in BDD + TDD **double loop**: a failing Reqnroll
scenario (tagged with its Plane issue, `@WHISPER-<id>`) drives xUnit red-green-refactor until green.
An issue is **Done** only when every acceptance criterion maps to a passing `@WHISPER-<id>` scenario.
The full strategy is in **[docs/bdd-strategy.md](docs/bdd-strategy.md)**.

## Documentation

- **[AGENTS.md](AGENTS.md)** — canonical guidance for humans and agents (start here).
- [docs/architecture.md](docs/architecture.md) — Clean Architecture + CQRS layering.
- [docs/coding-standards.md](docs/coding-standards.md) — Mediator, Mapperly, FluentValidation rules.
- [docs/bdd-strategy.md](docs/bdd-strategy.md) — the BDD/TDD strategy and Definition of Done.
- [docs/plane.md](docs/plane.md) — the WHISPER issue workflow.
- [docs/packaging.md](docs/packaging.md) — self-contained build + Velopack installer & releases.
