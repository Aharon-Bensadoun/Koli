#Requires -Version 5.1
<#
.SYNOPSIS
    Bumps the app version (optional), publishes Koli, and produces MSIX, MSI, or portable packages.

.EXAMPLE
    .\scripts\Publish-Koli.ps1
    Increments the revision (4th number), then publishes the MSIX package.

.EXAMPLE
    .\scripts\Publish-Koli.ps1 -Version 1.0.2.0
    Sets the version explicitly, then publishes.

.EXAMPLE
    .\scripts\Publish-Koli.ps1 -Bump Build -NoBump
    Publishes without changing the version.

.EXAMPLE
    .\scripts\Publish-Koli.ps1 -Target Portable
    Publishes a portable zip instead of an MSIX package.

.EXAMPLE
    .\scripts\Publish-Koli.ps1 -Target Msi
    Publishes an MSI installer (requires WiX Toolset CLI 5.x).

.EXAMPLE
    .\scripts\Publish-Koli.ps1 -Unpackaged
    Alias for -Target Portable.
#>
[CmdletBinding()]
param(
    [string]$Version,
    [ValidateSet('Revision', 'Build', 'Minor', 'Major')]
    [string]$Bump = 'Revision',
    [switch]$NoBump,
    [ValidateSet('Msix', 'Msi', 'Portable')]
    [string]$Target = 'Msix',
    [switch]$Unpackaged
)

$ErrorActionPreference = 'Stop'

$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$PropsPath = Join-Path $RepoRoot 'Directory.Build.props'
$ManifestPath = Join-Path $RepoRoot 'Koli.WinUI\Package.appxmanifest'
$ProjectPath = Join-Path $RepoRoot 'Koli.WinUI\Koli.WinUI.csproj'
$DistPath = Join-Path $RepoRoot 'Koli.WinUI\dist'
$InstallerDir = Join-Path $RepoRoot 'scripts\installer'
$InstallerWxsPath = Join-Path $InstallerDir 'Koli.wxs'
$LicenseRtfPath = Join-Path $InstallerDir 'license.rtf'

if ($Unpackaged) {
    if ($PSBoundParameters.ContainsKey('Target') -and $Target -ne 'Msix') {
        throw "Use either -Target or -Unpackaged, not both."
    }

    $Target = 'Portable'
}

function Get-KoliVersion {
    if (-not (Test-Path $PropsPath)) {
        throw "Missing $PropsPath"
    }

    $content = Get-Content -Path $PropsPath -Raw
    if ($content -match '<Version>([^<]+)</Version>') {
        return $Matches[1].Trim()
    }

    throw "Could not read <Version> from Directory.Build.props"
}

function Test-VersionFormat {
    param([string]$Value)

    if ($Value -notmatch '^\d+\.\d+\.\d+\.\d+$') {
        throw "Version must be four numeric parts (major.minor.build.revision), e.g. 1.0.1.3. Got: $Value"
    }

    foreach ($part in $Value.Split('.')) {
        $n = [int]$part
        if ($n -lt 0 -or $n -gt 65535) {
            throw "Each version part must be between 0 and 65535 (MSIX limit). Got: $Value"
        }
    }
}

function Get-NextVersion {
    param(
        [string]$Current,
        [string]$Part
    )

    $segments = $Current.Split('.')
    if ($segments.Count -ne 4) {
        throw "Current version must have four parts: $Current"
    }

    $index = switch ($Part) {
        'Major' { 0 }
        'Minor' { 1 }
        'Build' { 2 }
        'Revision' { 3 }
    }

    $segments[$index] = ([int]$segments[$index] + 1).ToString()
    return ($segments -join '.')
}

function Set-KoliVersion {
    param([string]$NewVersion)

    Test-VersionFormat $NewVersion

    $propsContent = Get-Content -Path $PropsPath -Raw
    $propsContent = $propsContent -replace '(?<=<Version>)[^<]+(?=</Version>)', $NewVersion
    $propsContent = $propsContent -replace '(?<=<AssemblyVersion>)[^<]+(?=</AssemblyVersion>)', $NewVersion
    $propsContent = $propsContent -replace '(?<=<FileVersion>)[^<]+(?=</FileVersion>)', $NewVersion
    $propsContent = $propsContent -replace '(?<=<InformationalVersion>)[^<]+(?=</InformationalVersion>)', $NewVersion
    $utf8NoBom = New-Object System.Text.UTF8Encoding $false
    [System.IO.File]::WriteAllText($PropsPath, $propsContent.TrimEnd() + [Environment]::NewLine, $utf8NoBom)

    $manifestContent = Get-Content -Path $ManifestPath -Raw
    $manifestContent = $manifestContent -replace '(?<=<Identity[^>]*Version=")[^"]+(?=")', $NewVersion
    [System.IO.File]::WriteAllText($ManifestPath, $manifestContent.TrimEnd() + [Environment]::NewLine, $utf8NoBom)
}

function Enable-MsixSameVersionReinstall {
    $scripts = Get-ChildItem -Path (Join-Path $RepoRoot 'Koli.WinUI\bin') -Recurse -Filter 'Add-AppDevPackage.ps1' -ErrorAction SilentlyContinue |
        Where-Object { $_.Directory.Name -like '*_Test' } |
        Sort-Object LastWriteTime -Descending

    foreach ($script in $scripts) {
        $content = Get-Content -Path $script.FullName -Raw
        if ($content -notmatch 'ForceUpdateFromAnyVersion') {
            $content = $content.Replace(
                '-ForceApplicationShutdown',
                '-ForceApplicationShutdown -ForceUpdateFromAnyVersion'
            )
            $utf8NoBom = New-Object System.Text.UTF8Encoding $false
            [System.IO.File]::WriteAllText($script.FullName, $content, $utf8NoBom)
            Write-Host "Patched installer: $($script.FullName)"
        }
    }
}

function Get-KoliPublishDir {
    $publishDir = Get-ChildItem -Path (Join-Path $RepoRoot 'Koli.WinUI\bin') -Recurse -Directory -Filter 'publish' -ErrorAction SilentlyContinue |
        Where-Object { Test-Path (Join-Path $_.FullName 'Koli.exe') } |
        Sort-Object LastWriteTime -Descending |
        Select-Object -First 1

    if (-not $publishDir) {
        throw 'Could not locate the dotnet publish output folder containing Koli.exe.'
    }

    return $publishDir.FullName
}

function Ensure-WixToolset {
    $wix = Get-Command wix -ErrorAction SilentlyContinue
    if (-not $wix) {
        throw @"
WiX Toolset CLI is required for MSI builds.
Install WiX 5.x with:
  dotnet tool install --global wix --version 5.0.2
  wix extension add WixToolset.Util.wixext/5.0.2
  wix extension add WixToolset.UI.wixext/5.0.2
"@
    }

    $versionText = (& wix --version 2>&1 | Out-String).Trim()
    if ($versionText -match '^(\d+)') {
        $major = [int]$Matches[1]
        if ($major -ge 7) {
            throw "WiX v$major requires OSMF acceptance. Install WiX 5.x instead: dotnet tool install --global wix --version 5.0.2"
        }
    }

    $extensions = (& wix extension list 2>&1 | Out-String)
    if ($extensions -notmatch 'WixToolset\.Util\.wixext') {
        Write-Host 'Adding WiX Util extension...'
        & wix extension add WixToolset.Util.wixext/5.0.2
        if ($LASTEXITCODE -ne 0) {
            throw 'Failed to add WixToolset.Util.wixext. Run: wix extension add WixToolset.Util.wixext/5.0.2'
        }
    }

    $extensions = (& wix extension list 2>&1 | Out-String)
    if ($extensions -notmatch 'WixToolset\.UI\.wixext') {
        Write-Host 'Adding WiX UI extension...'
        & wix extension add WixToolset.UI.wixext/5.0.2
        if ($LASTEXITCODE -ne 0) {
            throw 'Failed to add WixToolset.UI.wixext. Run: wix extension add WixToolset.UI.wixext/5.0.2'
        }
    }
}

function New-KoliPortableZip {
    param(
        [string]$PublishDir,
        [string]$AppVersion
    )

    New-Item -ItemType Directory -Path $DistPath -Force | Out-Null

    $zipName = "Koli_${AppVersion}_x64_portable.zip"
    $zipPath = Join-Path $DistPath $zipName
    if (Test-Path $zipPath) {
        Remove-Item -Path $zipPath -Force
    }

    Write-Host "Creating portable zip: $zipPath"
    Compress-Archive -Path (Join-Path $PublishDir '*') -DestinationPath $zipPath -CompressionLevel Optimal
    return $zipPath
}

function New-KoliMsi {
    param(
        [string]$PublishDir,
        [string]$AppVersion
    )

    Ensure-WixToolset

    New-Item -ItemType Directory -Path $DistPath -Force | Out-Null

    $msiName = "Koli_${AppVersion}_x64.msi"
    $msiPath = Join-Path $DistPath $msiName
    if (Test-Path $msiPath) {
        Remove-Item -Path $msiPath -Force
    }

    $wixArgs = @(
        'build',
        $InstallerWxsPath,
        '-ext', 'WixToolset.UI.wixext',
        '-ext', 'WixToolset.Util.wixext',
        '-d', "KoliVersion=$AppVersion",
        '-bindpath', "PublishPath=$PublishDir",
        '-bindvariable', "WixUILicenseRtf=$LicenseRtfPath",
        '-o', $msiPath
    )

    Write-Host "Running: wix $($wixArgs -join ' ')"
    & wix @wixArgs
    if ($LASTEXITCODE -ne 0) {
        exit $LASTEXITCODE
    }

    return $msiPath
}

$currentVersion = Get-KoliVersion

if ($Version) {
    if ($NoBump) {
        throw "Use either -Version or -NoBump, not both."
    }

    $targetVersion = $Version.Trim()
}
elseif ($NoBump) {
    $targetVersion = $currentVersion
    Write-Host "Keeping version $targetVersion"
}
else {
    $targetVersion = Get-NextVersion -Current $currentVersion -Part $Bump
    Write-Host "Bumping $Bump : $currentVersion -> $targetVersion"
}

if ($targetVersion -ne $currentVersion) {
    Set-KoliVersion -NewVersion $targetVersion
}

$publishArgs = @(
    'publish',
    $ProjectPath,
    '-c', 'Release',
    '-r', 'win-x64'
)

if ($Target -eq 'Msix') {
    $publishArgs += '-p:WindowsPackageType=MSIX'
}

Write-Host "Target: $Target"
Write-Host "Running: dotnet $($publishArgs -join ' ')"
& dotnet @publishArgs
if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}

switch ($Target) {
    'Msix' {
        Enable-MsixSameVersionReinstall

        $packageDir = Get-ChildItem -Path (Join-Path $RepoRoot 'Koli.WinUI\bin') -Recurse -Directory -Filter '*_Test' -ErrorAction SilentlyContinue |
            Sort-Object LastWriteTime -Descending |
            Select-Object -First 1

        if ($packageDir) {
            Write-Host ""
            Write-Host "MSIX package folder: $($packageDir.FullName)"
            Get-ChildItem -Path $packageDir.FullName -Filter '*.msix' | ForEach-Object {
                Write-Host "  $($_.Name)"
            }
        }
    }

    'Portable' {
        $publishDir = Get-KoliPublishDir
        $zipPath = New-KoliPortableZip -PublishDir $publishDir -AppVersion $targetVersion
        Write-Host ""
        Write-Host "Portable zip: $zipPath"
        Write-Host "Publish folder: $publishDir"
    }

    'Msi' {
        $publishDir = Get-KoliPublishDir
        $msiPath = New-KoliMsi -PublishDir $publishDir -AppVersion $targetVersion
        Write-Host ""
        Write-Host "MSI installer: $msiPath"
    }
}

Write-Host ""
Write-Host "Done. Version: $targetVersion"
