#requires -Version 7
<#
.SYNOPSIS
    Generates a self-signed Authenticode code-signing certificate for personal/dev signing.

.DESCRIPTION
    For a personal project, this mints a throwaway self-signed code-signing certificate and surfaces the
    two values build/pack.ps1 already consumes:

        VELOPACK_SIGN_CERTIFICATE   base64 of a password-protected PFX
        VELOPACK_SIGN_PASSWORD      the PFX password

    By default it sets those as process environment variables for the current PowerShell session, so the
    next `pwsh ./build/pack.ps1` produces a signed installer. The PFX is written to a path you control (or
    a temp file); no certificate or private key is ever committed — `*.pfx`/`*.p12`/`*.pem` are git-ignored.

    HONEST LIMITATION: a self-signed certificate only makes the signature valid on machines where the
    certificate is trusted (use -Trust to import it locally). It does NOT earn Windows SmartScreen
    reputation — first-run SmartScreen may still warn ("More info -> Run anyway"). A SmartScreen-trusted
    "verified publisher" requires a CA-issued OV/EV certificate or Azure Trusted Signing. This script
    is for local/personal builds, not for publicly distributed releases.

.PARAMETER Password
    The PFX password. If omitted, a random strong password is generated and printed once.

.PARAMETER Subject
    The certificate subject (publisher name shown by UAC). Default: "CN=Whisper (Self-Signed)".

.PARAMETER PfxPath
    Where to write the PFX. Default: a temp file. The directory is created if missing.

.PARAMETER Trust
    Also import the certificate into the CurrentUser Trusted Root + Trusted Publisher stores so
    `signtool verify /pa` passes and UAC shows the publisher on this machine. Trusting a self-signed root
    is a local-machine trust decision — only do this on your own machine.

.PARAMETER GitHubSecrets
    Print ready-to-run `gh secret set` commands (values redacted) instead of exporting env vars, for wiring
    the same self-signed cert into the release workflow.

.EXAMPLE
    pwsh ./build/new-self-signed-cert.ps1 -Trust
    pwsh ./build/pack.ps1          # produces a signed Whisper.Net-win-Setup.exe

.EXAMPLE
    pwsh ./build/new-self-signed-cert.ps1 -Password 'My$trongPass' -PfxPath ./artifacts/whisper-sign.pfx
#>
[CmdletBinding()]
param(
    [string]$Password,
    [string]$Subject = 'CN=Whisper (Self-Signed)',
    [string]$PfxPath,
    [switch]$Trust,
    [switch]$GitHubSecrets
)

$ErrorActionPreference = 'Stop'

if (-not $IsWindows) {
    throw 'A self-signed Authenticode certificate can only be generated on Windows (New-SelfSignedCertificate).'
}

# A random strong password when none is supplied, so the PFX is never left key-only.
if (-not $Password) {
    $bytes = [byte[]]::new(18)
    [System.Security.Cryptography.RandomNumberGenerator]::Fill($bytes)
    $Password = [Convert]::ToBase64String($bytes)
    Write-Host "Generated PFX password (store it safely): $Password" -ForegroundColor Yellow
}

if (-not $PfxPath) {
    $PfxPath = Join-Path ([System.IO.Path]::GetTempPath()) ("whisper-sign-" + [System.Guid]::NewGuid().ToString('N') + '.pfx')
}
$pfxDir = Split-Path -Parent $PfxPath
if ($pfxDir -and -not (Test-Path $pfxDir)) { New-Item -ItemType Directory -Force $pfxDir | Out-Null }

# 1. Mint a self-signed code-signing certificate (Code Signing EKU) in the current user's store.
$cert = New-SelfSignedCertificate `
    -Type CodeSigningCert `
    -Subject $Subject `
    -KeyUsage DigitalSignature `
    -KeyExportPolicy Exportable `
    -CertStoreLocation 'Cert:\CurrentUser\My' `
    -NotAfter (Get-Date).AddYears(3) `
    -HashAlgorithm SHA256
Write-Host "Created self-signed code-signing certificate: $($cert.Thumbprint) ($Subject)" -ForegroundColor Cyan

$securePassword = ConvertTo-SecureString -String $Password -Force -AsPlainText

try {
    # 2. Export a password-protected PFX and base64-encode it — the exact form VELOPACK_SIGN_CERTIFICATE wants.
    Export-PfxCertificate -Cert $cert -FilePath $PfxPath -Password $securePassword | Out-Null
    $base64 = [Convert]::ToBase64String([System.IO.File]::ReadAllBytes($PfxPath))

    # 3. (Opt-in) Trust the certificate locally so `signtool verify /pa` passes and UAC shows the publisher.
    #    Self-signed trust is a local decision; only the current user's stores are touched (no admin needed).
    if ($Trust) {
        foreach ($store in 'Root', 'TrustedPublisher') {
            Import-Certificate -FilePath (Export-Certificate -Cert $cert -FilePath (Join-Path $pfxDir "whisper-sign-pub.cer") -Force).FullName `
                -CertStoreLocation "Cert:\CurrentUser\$store" | Out-Null
        }
        Remove-Item -Force (Join-Path $pfxDir 'whisper-sign-pub.cer') -ErrorAction SilentlyContinue
        Write-Host 'Trusted the certificate in CurrentUser Root + TrustedPublisher; `signtool verify /pa` will now pass.' -ForegroundColor Cyan
    }

    if ($GitHubSecrets) {
        # Wire the same self-signed cert into the release workflow (values redacted from the console).
        Write-Host 'Run these to store the cert as repo secrets (values are not printed):' -ForegroundColor Cyan
        Write-Host '  gh secret set VELOPACK_SIGN_CERTIFICATE --body <base64-pfx>'
        Write-Host '  gh secret set VELOPACK_SIGN_PASSWORD    --body <password>'
        $env:VELOPACK_SIGN_CERTIFICATE = $base64
        $env:VELOPACK_SIGN_PASSWORD = $Password
    }
    else {
        # Default: export to the current session so the next build/pack.ps1 run signs with this cert.
        $env:VELOPACK_SIGN_CERTIFICATE = $base64
        $env:VELOPACK_SIGN_PASSWORD = $Password
        Write-Host 'Set VELOPACK_SIGN_CERTIFICATE + VELOPACK_SIGN_PASSWORD for this session.' -ForegroundColor Green
        Write-Host 'Next: pwsh ./build/pack.ps1   # produces a signed installer in ./releases' -ForegroundColor Green
    }

    Write-Host ''
    Write-Host 'Note: self-signed signing is trusted only where the cert is installed; it does not bypass' -ForegroundColor Yellow
    Write-Host 'Windows SmartScreen. A SmartScreen-verified publisher needs a CA cert.' -ForegroundColor Yellow
}
finally {
    $securePassword.Dispose()
}
