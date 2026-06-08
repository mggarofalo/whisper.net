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

## Bundled model assets (WHISPER-66)

The Silero **voice-activity-detection** model ships with the app as a committed content asset — there is
**no runtime download** (satisfies WHISPER-31 AC6):

| | |
|---|---|
| File | `assets/silero_vad.onnx` |
| Source | [`snakers4/silero-vad`](https://github.com/snakers4/silero-vad) tag `v4.0` (`files/silero_vad.onnx`) |
| License | MIT |
| Size | 1,807,522 bytes (~1.8 MB) |
| SHA-256 | `A35EBF52FD3CE5F1469B2A36158DBA761BC47B973EA3382B3186CA15B1F5AF28` |

The asset is declared once as `Content` in
[`src/Infrastructure/Infrastructure.csproj`](../src/Infrastructure/Infrastructure.csproj); MSBuild copies
it to `assets/silero_vad.onnx` in the build output of every referencing project and into the Velopack
single-file publish (next to the exe), which is where `OnnxVadSession` resolves it
(`AppContext.BaseDirectory/assets`). `OnnxVadSession` loads it lazily and runs inference fully on-device.
The `v4.0` model is required: it exposes the `h`/`c` recurrent-state I/O the session is built against; the
newer v5 single-`state` model would not load. Real inference over the bundled asset is covered by the
`@slow` `VadRealModelTests` (run in CI by the dedicated slow-tests step).

### Useful options

```pwsh
pwsh ./build/pack.ps1 -OutputDir ./releases -PublishDir ./artifacts/publish
```

## Velopack install/update hooks

`App.OnStartup` calls `VelopackApp.Build().Run()` first, so when the installer or updater launches the
app with a hook argument it performs the hook and exits instead of starting the tray.

## Auto-update (WHISPER-29)

When the user opts in (`AutoUpdate` settings — **off by default**, so there is no network egress
otherwise), a startup background check asks the release feed for a newer version and stages it to apply on
the next restart. The policy (`AutoUpdateService`) keeps the app running on the current version if the
channel is unreachable. See the network disclosure in [README](../README.md) and [CHANGELOG](../CHANGELOG.md).

## Code signing (WHISPER-29)

`build/pack.ps1` Authenticode-signs the app and installer through `signtool` (via `vpk --signParams`)
**when** a signing certificate is supplied from the environment — a base64 PFX in
`VELOPACK_SIGN_CERTIFICATE` plus `VELOPACK_SIGN_PASSWORD`. The release CI injects these from GitHub
Actions secrets; they are never committed and never echoed. Absent the secret, the build is unsigned.

## Tag-driven release CI (WHISPER-39)

`.github/workflows/release.yml` runs on a `vX.Y.Z` tag: it builds (`-warnaserror`), tests, packages, and
publishes the installer + update package + feed to a GitHub Release for the tag. A failing build or test
fails the job before anything is published.

## Verifying a build

`build/pack.ps1` produces the installer locally. The published single-file exe can be smoke-checked
without installing by running the built-in diagnostics, which load the native assets:

```pwsh
./artifacts/publish/Presentation.exe --doctor
```

A launch on a **pristine machine with no .NET runtime installed** is verified separately (a clean-VM
smoke test) — the self-contained bundle makes it true by construction.
