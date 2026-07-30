# whisper.net

**Talk to your PC, get text anywhere.** whisper.net is a local, GPU-accelerated speech-to-text
**dictation app for Windows**. It lives in your system tray: hold a hotkey, speak, and the words you
said are typed straight into whatever window has focus — your editor, browser, chat, email, anything.

Transcription runs **entirely on your machine** — your voice never leaves your computer.

> 🔒 **Private by default.** No audio, transcripts, or any of your data leave the device without an
> explicit action you take. There is no telemetry, no account, and no cloud. See
> [Privacy & network use](#privacy--network-use) for the few opt-in features that can use the network
> (all off by default).

---

## Requirements

- **Windows 10 or 11 (64-bit).**
- A **microphone**.
- That's it — the installer bundles everything else (including the .NET runtime). A modern GPU is used
  automatically for faster transcription if one is available, but it is **not required** — the app
  falls back to your CPU.

## Install

1. Go to the [**Releases**](https://github.com/mggarofalo/whisper.net/releases/latest) page.
2. Download **`Whisper.Net-win-Setup.exe`** and run it. It installs per-user (no admin prompt) and
   launches automatically.
   - Prefer not to install? Grab **`Whisper.Net-win-Portable.zip`** instead and run the app from the
     extracted folder.

When it starts, look for the whisper.net icon in your **system tray** (bottom-right of the taskbar,
near the clock).

## First run

A short **setup walkthrough** opens the first time you launch the app. It helps you:

1. **Pick your microphone** (capture device).
2. **Choose a speech model** and download it. Smaller models are faster; larger models are more
   accurate. The default, **`base.en`**, is a good balance for English on most machines. If you want
   noticeably better accuracy without giving up speed, pick a **Large v3 Turbo** build — same encoder as
   `large-v3` with a much cheaper decoder.
3. **Confirm your dictation hotkey** (default: **`Ctrl + Win`**).

You can change any of these later in **Settings**.

## How to use it

1. **Hold your hotkey** (`Ctrl + Win` by default) and **speak**.
2. **Release** the keys. whisper.net transcribes what you said and **types it into the focused field**.
3. Made a false start? Press **`Esc`** while recording to cancel — nothing is typed.

A small **overlay** appears while you talk, showing your live microphone level (and a brief "warming
up" note the first time, while the model loads). The app also supports **toggle** and **continuous**
dictation styles for longer hands-free passages; press **`Esc`** to leave continuous mode.

To open settings or quit, **right-click the tray icon** → **Open Settings** / **Quit**.

## Features

- **Local, on-device transcription** — fast, private, works offline.
- **Automatic GPU acceleration** (Vulkan) with seamless **CPU fallback** — no CUDA toolkit needed.
- **Type into any app** via simulated keystrokes, with an automatic **clipboard-paste fallback** when
  direct typing isn't possible.
- **Multiple Whisper models** to choose from (`tiny` → `large-v3`, English-only and multilingual
  variants, plus the fast **`large-v3-turbo`** builds), downloaded on demand and cached locally.
- **Rebindable global hotkey**, with push-to-talk, toggle, and continuous dictation.
- **Live level overlay** while recording.
- **History** of your transcriptions, with search, paging, and one-click re-copy.
- **Usage stats** — totals and a per-day breakdown.
- **Text clean-up** — strip filler words ("um", "uh"), apply a **custom vocabulary** to bias the model
  toward your domain terms, and run **output transforms** (formatting).
- **Audio cues** (optional) at recording start/stop and when transcription finishes.
- **Light / Dark / System theme**, and a keyboard- and screen-reader-accessible UI.
- **Run on login** (optional).
- **Built-in diagnostics** — launch with `--doctor` to run environment self-checks (microphone, model
  cache, hotkey, GPU) and print a pass/fail report.

## Settings

Right-click the tray icon → **Open Settings** to reach everything:

| Area | What you can do |
|------|-----------------|
| **Home** | A live status dashboard (active model, recent activity). |
| **Models** | Browse, download, and switch the active speech model. |
| **General** | Capture device, dictation hotkey, theme, run-on-login. |
| **Text processing** | Filler-word removal, custom vocabulary, output transforms, optional AI rephrase. |
| **History** | Browse, search, and re-copy past transcriptions; purge your data. |
| **Stats** | Usage totals and per-day breakdown. |

Settings apply **instantly** — no restart needed.

## Privacy & network use

Privacy is the point. Transcription is always **100% local**. The only times whisper.net can touch the
network are these **opt-in** features, each **off by default** and each clearly disclosed:

- **Model download** — when *you* choose a model, it is downloaded once from
  [Hugging Face](https://huggingface.co/ggerganov/whisper.cpp) and cached locally. Nothing is uploaded;
  after caching, transcription is fully offline.
- **AI rephrase (localhost-only)** — an optional step can rewrite recognized text with a locally-hosted
  [Ollama](https://ollama.com) model. It is **disabled by default** and may target **only** a loopback
  address (`localhost` / `127.0.0.1` / `[::1]`); a remote endpoint is refused. Your text never leaves
  the machine. If the local model isn't running, your original text is kept unchanged.
- **Automatic updates** — the app can check the project's [GitHub Releases](https://github.com/mggarofalo/whisper.net/releases)
  for a newer **signed** version and install it. It is **disabled by default**; when enabled, the
  release feed is the **only** thing contacted and **no user data is ever sent**.

There is **no telemetry**. Your transcription **history** is stored in a local database, and a more
verbose **audit log** is off unless you enable it. You can **purge** both from disk at any time from the
History settings.

## Where your data lives

- **Logs & model cache:** `%LOCALAPPDATA%\whisper-net\`
- **Settings & history database:** `%APPDATA%\whisper-net\`

## Updating

If automatic updates are enabled (Settings → it's off by default), the app updates itself in the
background and applies the update on the next restart. Otherwise, download the latest
`Whisper.Net-win-Setup.exe` from [Releases](https://github.com/mggarofalo/whisper.net/releases/latest)
and run it over your existing install.

## Uninstall

Uninstall **whisper.net** from **Windows Settings → Apps → Installed apps** like any other app. To also
remove your personal data, delete the two folders listed under
[Where your data lives](#where-your-data-lives).

## Troubleshooting

- **Nothing is typed.** Make sure a text field is focused when you release the hotkey. Some elevated
  (admin) windows block simulated input; the app falls back to clipboard paste where it can.
- **Run the doctor.** From a terminal in the install folder, launch the app with `--doctor` to check
  your microphone, model cache, hotkey registration, and GPU, and print a clear pass/fail report — the
  best thing to attach to a bug report.
- **Check the logs** in `%LOCALAPPDATA%\whisper-net\`.

Found a bug or have an idea? Please [open an issue](https://github.com/mggarofalo/whisper.net/issues).

## For developers

whisper.net is built ground-up on **.NET 10 + WPF** using Clean Architecture + CQRS, and is developed
test-first (BDD with Reqnroll + xUnit) under an AI-driven workflow tracked in Plane. The build,
contribution, and architecture guides live alongside the code:

- **[AGENTS.md](AGENTS.md)** — canonical guidance for humans and AI agents (start here).
- [docs/build-and-run.md](docs/build-and-run.md) — build from source, self-signed signing, and running.
- [docs/architecture.md](docs/architecture.md) — Clean Architecture + CQRS layering.
- [docs/packaging.md](docs/packaging.md) — the self-contained Velopack installer and tag-driven releases.
- [docs/bdd-strategy.md](docs/bdd-strategy.md) — the BDD/TDD strategy and Definition of Done.
- [docs/coding-standards.md](docs/coding-standards.md) — Mediator, Mapperly, and FluentValidation rules.
- [docs/plane.md](docs/plane.md) — the issue workflow.
- [CHANGELOG.md](CHANGELOG.md) — release history.

It is a ground-up rewrite of the Python [`whisper-local`](https://github.com/drajb/whisper-local) app.

## License

See [LICENSE](LICENSE).
