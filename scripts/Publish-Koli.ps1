#Requires -Version 5.1
<#
.SYNOPSIS
    Bumps the app version (optional), publishes Koli, and patches the MSIX installer for same-version reinstall.

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
    .\scripts\Publish-Koli.ps1 -Unpackaged
    Publishes a portable folder instead of an MSIX package.
#>
[CmdletBinding()]
param(
    [string]$Version,
    [ValidateSet('Revision', 'Build', 'Minor', 'Major')]
    [string]$Bump = 'Revision',
    [switch]$NoBump,
    [switch]$Unpackaged
)

$ErrorActionPreference = 'Stop'

$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$PropsPath = Join-Path $RepoRoot 'Directory.Build.props'
$ManifestPath = Join-Path $RepoRoot 'Koli.WinUI\Package.appxmanifest'
$ProjectPath = Join-Path $RepoRoot 'Koli.WinUI\Koli.WinUI.csproj'

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

if (-not $Unpackaged) {
    $publishArgs += '-p:WindowsPackageType=MSIX'
}

Write-Host "Running: dotnet $($publishArgs -join ' ')"
& dotnet @publishArgs
if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}

if (-not $Unpackaged) {
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

Write-Host ""
Write-Host "Done. Version: $targetVersion"
