#requires -Version 7
<#
.SYNOPSIS
    Builds the Whisper installer with Velopack (WHISPER-20).

.DESCRIPTION
    The one-command local packaging path: from a clean clone, `pwsh ./build/pack.ps1` produces a
    self-contained, single-file Windows build of the WPF tray app (the .NET 10 runtime and the native
    assets — Whisper.net + Vulkan, ONNX Runtime, SharpHook — bundled) and runs `vpk pack` to emit a
    Velopack release: an installer (`*-Setup.exe`), the update package, and the release feed.

    The version is derived from git tags by MinVer (never hand-edited): an exact `vX.Y.Z` tag produces
    that version; an untagged commit produces a pre-release. The `vpk` and `minver` CLIs are pinned in
    `.config/dotnet-tools.json` and restored on demand, so the build is reproducible.

.EXAMPLE
    pwsh ./build/pack.ps1
    pwsh ./build/pack.ps1 -Runtime win-x64 -OutputDir ./releases
#>
[CmdletBinding()]
param(
    [string]$Runtime = 'win-x64',
    [string]$Configuration = 'Release',
    [string]$PackId = 'Whisper.Net',
    [string]$PackTitle = 'Whisper',
    [string]$OutputDir,
    [string]$PublishDir
)

$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path "$PSScriptRoot/..").Path
$project = Join-Path $repo 'src/Presentation/Presentation.csproj'
$icon = Join-Path $repo 'assets/whisper.ico'
if (-not $OutputDir) { $OutputDir = Join-Path $repo 'releases' }
if (-not $PublishDir) { $PublishDir = Join-Path $repo 'artifacts/publish' }

Push-Location $repo
try {
    # The pinned vpk + minver CLIs (.config/dotnet-tools.json).
    dotnet tool restore | Out-Null

    # 1. Version from MinVer (git tags) — the single source of truth, never a hand-edited string. The
    #    tag prefix and the pre-release floor are read from Directory.Build.props so the packaged version
    #    always matches the assembly version the build stamps.
    $tagPrefix = (dotnet msbuild $project -getProperty:MinVerTagPrefix -v:quiet).Trim()
    $minMajorMinor = (dotnet msbuild $project -getProperty:MinVerMinimumMajorMinor -v:quiet).Trim()
    $minverArgs = @('--tag-prefix', $tagPrefix)
    if ($minMajorMinor) { $minverArgs += @('--minimum-major-minor', $minMajorMinor) }
    $version = (dotnet minver @minverArgs).Trim()
    if (-not $version) { throw 'Could not resolve the MinVer version.' }
    # Velopack wants a clean SemVer core; drop any build metadata after '+'.
    $packVersion = ($version -split '\+')[0]
    Write-Host "Packaging $PackId $packVersion ($Runtime)" -ForegroundColor Cyan

    # 2. Self-contained, single-file publish with the runtime + native assets bundled. The csproj turns
    #    on SelfContained / PublishSingleFile / native self-extract whenever a RID is supplied.
    if (Test-Path $PublishDir) { Remove-Item -Recurse -Force $PublishDir }
    dotnet publish $project -c $Configuration -r $Runtime -o $PublishDir --nologo
    if ($LASTEXITCODE -ne 0) { throw 'dotnet publish failed.' }

    # 3. Velopack release: installer (*-Setup.exe) + update package + feed, stamped with the MinVer version
    #    and the app id, title, and icon.
    New-Item -ItemType Directory -Force $OutputDir | Out-Null
    dotnet vpk pack `
        --packId $PackId `
        --packTitle $PackTitle `
        --packVersion $packVersion `
        --packDir $PublishDir `
        --mainExe 'Presentation.exe' `
        --icon $icon `
        --outputDir $OutputDir
    if ($LASTEXITCODE -ne 0) { throw 'vpk pack failed.' }

    Write-Host "Release artifacts written to $OutputDir" -ForegroundColor Green
    Get-ChildItem $OutputDir | Select-Object Name, Length | Format-Table -AutoSize
}
finally {
    Pop-Location
}
