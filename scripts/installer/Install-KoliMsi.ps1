#Requires -Version 5.1
[CmdletBinding()]
param(
    [switch]$Help,
    [string]$MsiPath,
    [string]$ApiKey,
    [string]$Endpoint,
    [ValidateRange(0, [int]::MaxValue)]
    [int]$ProviderId,
    [string]$Model,
    [string]$ProfileName = 'Default',
    [string]$TargetUser,
    [string]$TargetUserSid,
    [switch]$OverwriteProfile,
    [switch]$DoNotSetDefaultProfile,
    [switch]$Interactive,
    [string]$LogPath
)

$ErrorActionPreference = 'Stop'

function Test-KoliAdmin {
    $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = [Security.Principal.WindowsPrincipal]$identity
    return $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}

function Request-KoliAdmin {
    param([System.Collections.IDictionary]$BoundParameters)

    $argList = @(
        '-NoProfile',
        '-ExecutionPolicy', 'Bypass',
        '-File', $PSCommandPath
    )
    foreach ($key in $BoundParameters.Keys) {
        $value = $BoundParameters[$key]
        if ($value -is [switch]) {
            if ($value) { $argList += "-$key" }
        }
        else {
            $argList += "-$key"
            $argList += "$value"
        }
    }

    $process = Start-Process -FilePath 'powershell.exe' -Verb RunAs -ArgumentList $argList -Wait -PassThru
    exit $process.ExitCode
}

function Show-KoliMsiHelp {
    @'
Koli MSI command-line installer

Usage:
  .\Install-KoliMsi.ps1 -Help
  .\Install-KoliMsi.ps1 -MsiPath .\Koli_x64.msi [options]

Configuration options:
  -ApiKey <value>          OpenAI, Azure OpenAI, or AI Nexus API key
  -Endpoint <url>          Transcription endpoint
  -ProviderId <number>     Optional AI Nexus provider ID
  -Model <name>            Optional transcription model
  -ProfileName <name>      Profile name (default: Default)
  -TargetUser <account>    Windows account, for example DOMAIN\user
  -TargetUserSid <sid>     Preferred explicit Windows SID
  -OverwriteProfile        Replace an existing profile with the same name
  -DoNotSetDefaultProfile  Do not activate the provisioned profile by default

Installer options:
  -Interactive             Show the MSI user interface (silent by default)
  -LogPath <path>          Write a verbose Windows Installer log

Example:
  .\Install-KoliMsi.ps1 -MsiPath .\Koli_x64.msi `
    -ApiKey "secret" `
    -Endpoint "https://nexus.example.com/api/AI/queryAudio" `
    -ProviderId 7 `
    -Model "whisper-1" `
    -ProfileName "Medical" `
    -TargetUserSid "S-1-5-21-..."

Security:
  API keys passed on a command line can be observed by privileged local
  processes. Koli hides the key from MSI logs and encrypts it with DPAPI when
  the target user first launches the application.
'@
}

function ConvertTo-MsiPropertyArgument {
    param([string]$Name, [string]$Value)
    if ($Value.Contains('"')) {
        throw "$Name cannot contain a double quote."
    }
    return "$Name=`"$Value`""
}

if ($Help) {
    Show-KoliMsiHelp
    exit 0
}

if (-not (Test-KoliAdmin)) {
    Request-KoliAdmin -BoundParameters $PSBoundParameters
}

if (-not $MsiPath) {
    $candidates = @(Get-ChildItem -LiteralPath $PSScriptRoot -Filter 'Koli_*_x64*.msi' -File)
    if ($candidates.Count -ne 1) {
        throw 'Specify -MsiPath. Automatic detection requires exactly one Koli MSI beside this script.'
    }
    $MsiPath = $candidates[0].FullName
}

$resolvedMsi = (Resolve-Path -LiteralPath $MsiPath).Path
if ([IO.Path]::GetExtension($resolvedMsi) -ne '.msi') {
    throw "MsiPath must reference an .msi file: $resolvedMsi"
}
if ($Endpoint -and -not [Uri]::IsWellFormedUriString($Endpoint, [UriKind]::Absolute)) {
    throw "Endpoint must be an absolute URL: $Endpoint"
}
if ($TargetUser -and $TargetUserSid) {
    throw 'Use either -TargetUser or -TargetUserSid, not both.'
}

$arguments = @('/i', "`"$resolvedMsi`"")
$arguments += if ($Interactive) { '/passive' } else { '/qn' }
if ($PSBoundParameters.ContainsKey('ApiKey')) { $arguments += ConvertTo-MsiPropertyArgument 'KOLI_API_KEY' $ApiKey }
if ($PSBoundParameters.ContainsKey('Endpoint')) { $arguments += ConvertTo-MsiPropertyArgument 'KOLI_ENDPOINT' $Endpoint }
if ($PSBoundParameters.ContainsKey('ProviderId')) { $arguments += ConvertTo-MsiPropertyArgument 'KOLI_PROVIDER_ID' $ProviderId }
if ($PSBoundParameters.ContainsKey('Model')) { $arguments += ConvertTo-MsiPropertyArgument 'KOLI_MODEL' $Model }
if ($PSBoundParameters.ContainsKey('ProfileName')) { $arguments += ConvertTo-MsiPropertyArgument 'KOLI_PROFILE_NAME' $ProfileName }
if ($TargetUser) { $arguments += ConvertTo-MsiPropertyArgument 'KOLI_TARGET_USER' $TargetUser }
if ($TargetUserSid) { $arguments += ConvertTo-MsiPropertyArgument 'KOLI_TARGET_USER_SID' $TargetUserSid }
if ($OverwriteProfile) { $arguments += 'KOLI_OVERWRITE_PROFILE=1' }
if ($DoNotSetDefaultProfile) { $arguments += 'KOLI_SET_DEFAULT_PROFILE=0' }
if ($LogPath) { $arguments += @('/l*v', "`"$LogPath`"") }

$process = Start-Process -FilePath 'msiexec.exe' -ArgumentList $arguments -Wait -PassThru
if ($process.ExitCode -notin 0, 1641, 3010) {
    throw "Koli MSI installation failed with exit code $($process.ExitCode)."
}

Write-Host "Koli MSI completed with exit code $($process.ExitCode)."
exit $process.ExitCode
