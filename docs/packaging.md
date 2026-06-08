# Packaging & releases

The Windows tray app ships as a **self-contained, single-file build** wrapped in a
[Velopack](https://velopack.io) installer, so an end user installs and runs it **without a separate
.NET install**. Packaging is reproducible from a clean clone with one command.

## One-command local packaging (WHISPER-20)

```pwsh
pwsh ./build/pack.ps1
```

This:

1. Restores the pinned CLIs from [`.config/dotnet-tools.json`](../.config/dotnet-tools.json) — `vpk`
   (Velopack) and `minver`.
2. Resolves the version from **MinVer** (git tags) — never a hand-edited string. A `vX.Y.Z` tag
   produces exactly `X.Y.Z`; an untagged commit produces a pre-release floored at `0.1.0` (see
   `MinVerMinimumMajorMinor` in [`Directory.Build.props`](../Directory.Build.props)).
3. Publishes `src/Presentation` self-contained and single-file for `win-x64`, bundling the .NET 10
   runtime and the **native assets** (Whisper.net + `Whisper.net.Runtime.Vulkan`, ONNX Runtime,
   SharpHook, SQLite). Native libraries are self-extracted next to the single file at first run.
4. Runs `vpk pack` to emit a Velopack release into `./releases/`:
   - `Whisper.Net-win-Setup.exe` — the installer
   - `Whisper.Net-<version>-full.nupkg` — the update package
   - `RELEASES` / `releases.win.json` — the update feed
   - `Whisper.Net-win-Portable.zip` — a portable build

The publish settings live in [`src/Presentation/Presentation.csproj`](../src/Presentation/Presentation.csproj)
under a `'$(RuntimeIdentifier)' != ''` group, so they apply only when publishing with `-r win-x64`; a
normal `dotnet build` is unaffected. The app id, title, and icon (`assets/whisper.ico`) are passed to
`vpk pack`.

### Useful options

```pwsh
pwsh ./build/pack.ps1 -OutputDir ./releases -PublishDir ./artifacts/publish
```

## Velopack install/update hooks

`App.OnStartup` calls `VelopackApp.Build().Run()` first, so when the installer or updater launches the
app with a hook argument it performs the hook and exits instead of starting the tray. The in-app
**auto-update** check and **code signing** are added in WHISPER-29; **tag-driven release CI** in
WHISPER-39.

## Verifying a build

`build/pack.ps1` produces the installer locally. The published single-file exe can be smoke-checked
without installing by running the built-in diagnostics, which load the native assets:

```pwsh
./artifacts/publish/Presentation.exe --doctor
```

A launch on a **pristine machine with no .NET runtime installed** is verified separately (a clean-VM
smoke test) — the self-contained bundle makes it true by construction.
