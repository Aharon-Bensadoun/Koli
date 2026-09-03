#Requires -Version 5.1
<#
.SYNOPSIS
    Signs Koli binaries (EXE, DLL, ...) and/or the MSI installer with an Authenticode
    code-signing certificate from the current user's personal store (Cert:\CurrentUser\My).

.DESCRIPTION
    Uses signtool.exe (from the Windows SDK) when available, and falls back to
    Set-AuthenticodeSignature otherwise. Always applies an RFC 3161 SHA-256 timestamp so
    the signature stays valid after the certificate expires.

    The certificate is selected by thumbprint. The company name shown as the "publisher"
    in Windows comes from the certificate's Subject (CN), so make sure the CA-issued
    certificate has the correct company common name.

    When a directory is passed, every unsigned .exe / .dll / .msi under it is signed.
    Files that already have a valid Authenticode signature (for example Microsoft
    runtime DLLs) are left untouched unless -Force is set.

.PARAMETER Path
    One or more files or directories to sign. Wildcards are supported.
    Directories are scanned recursively for signable binaries.
    If omitted, the script auto-discovers the published binaries and the latest MSI in dist.

.PARAMETER Thumbprint
    SHA-1 thumbprint of the code-signing certificate in Cert:\CurrentUser\My.

.PARAMETER TimestampServer
    RFC 3161 timestamp server URL.

.PARAMETER Force
    Re-sign files even if they already have a valid Authenticode signature.

.EXAMPLE
    .\scripts\Sign-Koli.ps1
    Auto-discovers and signs unsigned published binaries and the latest MSI.

.EXAMPLE
    .\scripts\Sign-Koli.ps1 -Path .\Koli.WinUI\bin\Release\net8.0-windows10.0.22621.0\win-x64\publish

.EXAMPLE
    .\scripts\Sign-Koli.ps1 -Path .\Koli.WinUI\dist\Koli_1.0.0.0_x64.msi

.EXAMPLE
    .\scripts\Sign-Koli.ps1 -Path .\path\to\app.exe, .\path\to\installer.msi -Thumbprint 250ef3d5376f8c880c94d48981d0b3df2f9a0345
#>
[CmdletBinding()]
param(
    [string[]]$Path,
    [string]$Thumbprint = '250ef3d5376f8c880c94d48981d0b3df2f9a0345',
    [string]$TimestampServer = 'http://timestamp.digicert.com',
    [switch]$Force
)

$ErrorActionPreference = 'Stop'

$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$DistPath = Join-Path $RepoRoot 'Koli.WinUI\dist'
$SignableExtensions = @('.exe', '.dll', '.msi', '.cab', '.ocx', '.sys')
$SignToolBatchSize = 20

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

function Test-SignableExtension {
    param([string]$FilePath)

    $ext = [IO.Path]::GetExtension($FilePath)
    return $SignableExtensions -contains $ext.ToLowerInvariant()
}

function Get-PublishDirectory {
    $publishDir = Get-ChildItem -Path (Join-Path $RepoRoot 'Koli.WinUI\bin') -Recurse -Directory -Filter 'publish' -ErrorAction SilentlyContinue |
        Where-Object { Test-Path (Join-Path $_.FullName 'Koli.exe') } |
        Sort-Object LastWriteTime -Descending |
        Select-Object -First 1

    if ($publishDir) {
        return $publishDir.FullName
    }

    return $null
}

function Get-SignableFilesFromDirectory {
    param([string]$Directory)

    return @(
        Get-ChildItem -Path $Directory -Recurse -File -ErrorAction SilentlyContinue |
            Where-Object { Test-SignableExtension -FilePath $_.FullName } |
            ForEach-Object { $_.FullName }
    )
}

function Select-FilesToSign {
    param(
        [string[]]$Files,
        [switch]$Force
    )

    $selected = @()
    $skipped = 0

    foreach ($file in ($Files | Sort-Object -Unique)) {
        if (-not (Test-Path -LiteralPath $file -PathType Leaf)) {
            continue
        }

        if (-not $Force) {
            $sig = Get-AuthenticodeSignature -FilePath $file
            if ($sig.Status -eq 'Valid') {
                $skipped += 1
                continue
            }
        }

        $selected += $file
    }

    return [pscustomobject]@{
        Files   = $selected
        Skipped = $skipped
    }
}

function Resolve-Targets {
    param([string[]]$Path)

    $resolved = @()

    if ($Path) {
        foreach ($p in $Path) {
            $matches = Resolve-Path -Path $p -ErrorAction SilentlyContinue
            if (-not $matches) {
                Write-Warning "No file matched: $p"
                continue
            }

            foreach ($match in $matches) {
                if (Test-Path -LiteralPath $match.Path -PathType Container) {
                    $resolved += Get-SignableFilesFromDirectory -Directory $match.Path
                }
                elseif (Test-SignableExtension -FilePath $match.Path) {
                    $resolved += $match.Path
                }
                else {
                    Write-Warning "Skipping unsupported file type: $($match.Path)"
                }
            }
        }

        return @($resolved | Sort-Object -Unique)
    }

    $publishDir = Get-PublishDirectory
    if ($publishDir) {
        $resolved += Get-SignableFilesFromDirectory -Directory $publishDir
    }

    if (Test-Path $DistPath) {
        $msi = Get-ChildItem -Path $DistPath -Filter '*.msi' -ErrorAction SilentlyContinue |
            Sort-Object LastWriteTime -Descending |
            Select-Object -First 1
        if ($msi) { $resolved += $msi.FullName }
    }

    $resolved = @($resolved | Sort-Object -Unique)
    if (-not $resolved) {
        throw "No files to sign were found. Publish first, or pass -Path explicitly."
    }

    return $resolved
}

function Invoke-SignToolSign {
    param(
        [string]$SignTool,
        [string]$Thumbprint,
        [string]$TimestampServer,
        [string[]]$Files
    )

    for ($i = 0; $i -lt $Files.Count; $i += $SignToolBatchSize) {
        $end = [Math]::Min($i + $SignToolBatchSize - 1, $Files.Count - 1)
        $batch = @($Files[$i..$end])

        $signArgs = @(
            'sign',
            '/sha1', $Thumbprint,
            '/fd', 'SHA256',
            '/tr', $TimestampServer,
            '/td', 'SHA256'
        ) + $batch

        Write-Host "signtool sign ($($batch.Count) file(s), batch $([int]($i / $SignToolBatchSize) + 1))"
        & $SignTool @signArgs
        if ($LASTEXITCODE -ne 0) {
            throw "signtool failed with exit code $LASTEXITCODE."
        }
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

$candidates = Resolve-Targets -Path $Path
$selection = Select-FilesToSign -Files $candidates -Force:$Force
$targets = @($selection.Files)

Write-Host "Signable files found: $($candidates.Count)"
if ($selection.Skipped -gt 0) {
    Write-Host "Already signed (skipped): $($selection.Skipped)"
}
Write-Host "Files to sign: $($targets.Count)"
$targets | ForEach-Object { Write-Host "  $_" }
Write-Host ""

if (-not $targets) {
    Write-Host "Nothing to sign."
    return
}

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
