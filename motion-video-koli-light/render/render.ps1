[CmdletBinding()]
param([switch]$OnlyCapture, [switch]$SkipCapture)

# Koli LIGHT motion video -> MP4  (Windows / PowerShell)
# Usage:  ./render.ps1                full render (capture + encode + mux audio)
#         ./render.ps1 -OnlyCapture   capture frames only
$ErrorActionPreference = "Stop"
Set-Location $PSScriptRoot

if (-not (Test-Path "node_modules")) {
  Write-Host "[render] Installing puppeteer (first run only)..."
  npm install
}

$extra = @()
if ($OnlyCapture) { $extra += "--only-capture" }
if ($SkipCapture) { $extra += "--skip-capture" }

node render.js @extra
Write-Host "[render] Output: ..\out\koli-promo-light.mp4"
