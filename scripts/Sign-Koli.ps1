#Requires -Version 5.1
<#
.SYNOPSIS
    Signs Koli binaries (EXE) and/or the MSI installer with an Authenticode code-signing
    certificate from the current user's personal certificate store (Cert:\CurrentUser\My).

.DESCRIPTION
    Uses signtool.exe (from the Windows SDK) when available, and falls back to
    Set-AuthenticodeSignature otherwise. Always applies an RFC 3161 SHA-256 timestamp so
    the signature stays valid after the certificate expires.

    The certificate is selected by thumbprint. The company name shown as the "publisher"
    in Windows comes from the certificate's Subject (CN), so make sure the CA-issued
    certificate has the correct company common name.

.PARAMETER Path
    One or more files to sign (EXE, DLL, MSI, ...). Wildcards are supported.
    If omitted, the script auto-discovers the published Koli.exe and the MSI in dist.

.PARAMETER Thumbprint
    SHA-1 thumbprint of the code-signing certificate in Cert:\CurrentUser\My.

.PARAMETER TimestampServer
    RFC 3161 timestamp server URL.

.EXAMPLE
    .\scripts\Sign-Koli.ps1
    Auto-discovers and signs the published Koli.exe and the latest MSI.

.EXAMPLE
    .\scripts\Sign-Koli.ps1 -Path .\Koli.WinUI\dist\Koli_1.0.0.0_x64.msi

.EXAMPLE
    .\scripts\Sign-Koli.ps1 -Path .\path\to\app.exe, .\path\to\installer.msi
#>
[CmdletBinding()]
param(
    [string[]]$Path,
    [string]$Thumbprint = '250ef3d5376f8c880c94d48981d0b3df2f9a0345',
    [string]$TimestampServer = 'http://timestamp.digicert.com'
)

$ErrorActionPreference = 'Stop'

$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$DistPath = Join-Path $RepoRoot 'Koli.WinUI\dist'

function Get-SigningCertificate {
    param([string]$Thumbprint)

    $clean = ($Thumbprint -replace '[^0-9A-Fa-f]', '').ToUpperInvariant()
    $cert = Get-ChildItem -Path Cert:\CurrentUser\My -CodeSigningCert -ErrorAction SilentlyContinue |
        Where-Object { $_.Thumbprint -eq $clean } |
        Select-Object -First 1

    if (-not $cert) {
        $cert = Get-Item -Path "Cert:\CurrentUser\My\$clean" -ErrorAction SilentlyContinue
    }

    if (-not $cert) {
        throw "No certificate with thumbprint '$clean' found in Cert:\CurrentUser\My. Verify it is a code-signing certificate and that its private key is available."
    }

    if (-not $cert.HasPrivateKey) {
        throw "Certificate '$($cert.Subject)' has no associated private key; it cannot be used to sign."
    }

    return $cert
}

function Find-SignTool {
    $cmd = Get-Command signtool.exe -ErrorAction SilentlyContinue
    if ($cmd) { return $cmd.Source }

    $roots = @(
        "${env:ProgramFiles(x86)}\Windows Kits\10\bin",
        "${env:ProgramFiles}\Windows Kits\10\bin"
    ) | Where-Object { $_ -and (Test-Path $_) }

    foreach ($root in $roots) {
        $found = Get-ChildItem -Path $root -Recurse -Filter 'signtool.exe' -ErrorAction SilentlyContinue |
            Where-Object { $_.FullName -match '\\x64\\' } |
            Sort-Object FullName -Descending |
            Select-Object -First 1
        if ($found) { return $found.FullName }
    }

    return $null
}

function Resolve-Targets {
    param([string[]]$Path)

    if ($Path) {
        $resolved = @()
        foreach ($p in $Path) {
            $matches = Resolve-Path -Path $p -ErrorAction SilentlyContinue
            if (-not $matches) {
                Write-Warning "No file matched: $p"
                continue
            }
            $resolved += $matches.Path
        }
        return $resolved | Sort-Object -Unique
    }

    # Auto-discovery
    $targets = @()

    $publishExe = Get-ChildItem -Path (Join-Path $RepoRoot 'Koli.WinUI\bin') -Recurse -Filter 'Koli.exe' -ErrorAction SilentlyContinue |
        Where-Object { $_.DirectoryName -match '\\publish$' } |
        Sort-Object LastWriteTime -Descending |
        Select-Object -First 1
    if ($publishExe) { $targets += $publishExe.FullName }

    if (Test-Path $DistPath) {
        $msi = Get-ChildItem -Path $DistPath -Filter '*.msi' -ErrorAction SilentlyContinue |
            Sort-Object LastWriteTime -Descending |
            Select-Object -First 1
        if ($msi) { $targets += $msi.FullName }
    }

    if (-not $targets) {
        throw "No files to sign were found. Publish first, or pass -Path explicitly."
    }

    return $targets | Sort-Object -Unique
}

function Invoke-SignToolSign {
    param(
        [string]$SignTool,
        [string]$Thumbprint,
        [string]$TimestampServer,
        [string[]]$Files
    )

    $signArgs = @(
        'sign',
        '/sha1', $Thumbprint,
        '/fd', 'SHA256',
        '/tr', $TimestampServer,
        '/td', 'SHA256'
    ) + $Files

    Write-Host "signtool $($signArgs -join ' ')"
    & $SignTool @signArgs
    if ($LASTEXITCODE -ne 0) {
        throw "signtool failed with exit code $LASTEXITCODE."
    }
}

function Invoke-PowerShellSign {
    param(
        [System.Security.Cryptography.X509Certificates.X509Certificate2]$Certificate,
        [string]$TimestampServer,
        [string[]]$Files
    )

    foreach ($file in $Files) {
        $result = Set-AuthenticodeSignature -FilePath $file -Certificate $Certificate `
            -HashAlgorithm SHA256 -TimestampServer $TimestampServer
        if ($result.Status -ne 'Valid') {
            throw "Signing failed for '$file': $($result.Status) - $($result.StatusMessage)"
        }
    }
}

# --- Main ---

$cert = Get-SigningCertificate -Thumbprint $Thumbprint
$cleanThumbprint = $cert.Thumbprint

Write-Host "Signing certificate:"
Write-Host "  Subject    : $($cert.Subject)"
Write-Host "  Issuer     : $($cert.Issuer)"
Write-Host "  Thumbprint : $cleanThumbprint"
Write-Host "  Valid until: $($cert.NotAfter)"
Write-Host ""

$targets = Resolve-Targets -Path $Path
Write-Host "Files to sign:"
$targets | ForEach-Object { Write-Host "  $_" }
Write-Host ""

$signTool = Find-SignTool
if ($signTool) {
    Write-Host "Using signtool: $signTool"
    Invoke-SignToolSign -SignTool $signTool -Thumbprint $cleanThumbprint `
        -TimestampServer $TimestampServer -Files $targets
}
else {
    Write-Host "signtool.exe not found; falling back to Set-AuthenticodeSignature."
    Invoke-PowerShellSign -Certificate $cert -TimestampServer $TimestampServer -Files $targets
}

Write-Host ""
Write-Host "Verifying signatures:"
foreach ($file in $targets) {
    $sig = Get-AuthenticodeSignature -FilePath $file
    Write-Host ("  [{0}] {1}" -f $sig.Status, $file)
    if ($sig.Status -ne 'Valid') {
        Write-Warning "  -> $($sig.StatusMessage)"
    }
}

Write-Host ""
Write-Host "Done."
