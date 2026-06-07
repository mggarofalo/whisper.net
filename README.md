# whisper.net

A local, GPU-accelerated speech-to-text **dictation utility for Windows**, built on .NET 10 + WPF.
It is tray-resident: a global hotkey records the microphone, [Whisper.net](https://github.com/sandrohanea/whisper.net)
transcribes **entirely on your machine**, and the recognized text is injected into whatever field has
focus. It is a ground-up rewrite of the Python [`whisper-local`](https://github.com/drajb/whisper-local) app.

> **Privacy:** transcription runs locally. No audio, transcripts, or user data ever leave the device
> without an explicit, opt-in action. There is no telemetry. See
> [AGENTS.md → Privacy stance](AGENTS.md#privacy-stance).

## Build and run

```bash
dotnet restore Whisper.slnx
dotnet build   Whisper.slnx -p:TreatWarningsAsErrors=true
dotnet test    Whisper.slnx --filter "Category!=wip"
npm install    # one-time: installs the commitlint commit-msg hook
```

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
