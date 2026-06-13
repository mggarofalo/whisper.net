# Build and run from source

This guide takes you from a clean clone to a running, optionally **self-signed** Whisper install. It
complements [docs/packaging.md](packaging.md) (the packaging/release internals) with the end-to-end
maintainer path: build → sign → package → install → run.

## Prerequisites

- **.NET 10 SDK** (the app targets `net10.0` / `net10.0-windows`).
- **Windows** for packaging, signing, and running the tray app (`win-x64`). The non-WPF projects build
  and test cross-platform, but the installer and signing are Windows-only.
- **PowerShell 7+** (`pwsh`) for the `build/*.ps1` scripts.
- **Node.js** (optional) for the commitlint commit-msg hook.

## 1. Build and test from source

```bash
dotnet restore Whisper.slnx
dotnet build   Whisper.slnx -p:TreatWarningsAsErrors=true
dotnet test    Whisper.slnx --filter "Category!=wip&Category!=slow"
npm install    # one-time: installs the commitlint commit-msg hook
```

`dotnet build` warns-as-errors exactly as CI does; `Category!=wip&Category!=slow` is the fast gate
(xUnit unit tests + Reqnroll `@WHISPER-<id>` scenarios), excluding the `@slow` real-model VAD tests —
run those with `--filter "Category=slow"`.

## 2. Package the installer

```pwsh
pwsh ./build/pack.ps1     # -> ./releases/Whisper.Net-win-Setup.exe (+ update package & feed)
```

This produces an **unsigned** self-contained, single-file installer. The version comes from git tags via
MinVer (never hand-edited). See [docs/packaging.md](packaging.md) for what `pack.ps1` does step by step.

## 3. (Optional) Sign with a self-signed certificate

For a personal project you can sign the app and installer with a **self-signed** certificate — no CA
purchase required. `build/new-self-signed-cert.ps1` mints the certificate and exports the two values
`build/pack.ps1` already consumes (`VELOPACK_SIGN_CERTIFICATE` = base64 PFX, `VELOPACK_SIGN_PASSWORD`):

```pwsh
pwsh ./build/new-self-signed-cert.ps1 -Trust   # mint, export, and trust it on this machine
pwsh ./build/pack.ps1                           # now produces a *signed* installer
```

`-Trust` imports the certificate into your `CurrentUser` Trusted Root + Trusted Publisher stores so the
signature validates locally. Verify it:

```pwsh
# signtool ships with the Windows SDK (e.g. C:\Program Files (x86)\Windows Kits\10\bin\<ver>\x64\signtool.exe)
signtool verify /pa /v .\releases\Whisper.Net-win-Setup.exe
```

After `-Trust`, `signtool verify /pa` passes and Windows UAC shows your publisher name (the certificate
subject) instead of "Unknown Publisher" on this machine.

> ⚠️ **Honest limitation — self-signed does not bypass SmartScreen.** A self-signed certificate is trusted
> only on machines where you have installed it. It earns **no Windows SmartScreen reputation**, so a
> freshly downloaded installer may still show a SmartScreen warning on first run ("More info → Run
> anyway"), and it is **not** valid for publicly distributed releases. A SmartScreen-trusted "verified
> publisher" requires a CA-issued OV/EV certificate or **Azure Trusted Signing** (tracked as a separate
> issue). Use self-signed signing for your own machines, not for shipping to other people.

To wire the same self-signed cert into the tag-driven release workflow instead of the local session:

```pwsh
pwsh ./build/new-self-signed-cert.ps1 -GitHubSecrets   # prints the gh secret set commands to run
```

The script never commits the certificate or key — `*.pfx`/`*.p12`/`*.pem` are git-ignored, and the PFX is
written to a path you choose (a temp file by default).

## 4. Install and run

Run the produced `./releases/Whisper.Net-win-Setup.exe` to install. The app installs per-user and starts
in the system tray. To smoke-check the published build **without** installing — it loads the bundled
native assets (NAudio, Vulkan, SharpHook, SQLite) on its own runtime:

```pwsh
./artifacts/publish/Presentation.exe --doctor
```

Because the build is self-contained, the installed app launches on a machine with **no separate .NET
runtime** installed.

## Logs & troubleshooting

The app writes a rolling log file (daily, 14 files retained) to:

```
%LOCALAPPDATA%\whisper.net\logs\whisper-<date>.log
```

Attach the most recent file to any bug report. Unhandled UI errors are recorded there and no longer take
the tray app down silently. For a one-shot environment check (audio, model cache, hotkeys, GPU/Vulkan),
run the published exe with `--doctor` (above).
