@echo off
setlocal EnableExtensions

net session >nul 2>&1
if errorlevel 1 (
    powershell -NoProfile -Command "Start-Process -FilePath '%~f0' -WorkingDirectory '%~dp0' -Verb RunAs"
    exit /b
)

set "MSI="
for %%F in ("%~dp0Koli_*_x64*.msi") do set "MSI=%%~fF"

if not defined MSI (
    echo No Koli MSI found next to this script.
    pause
    exit /b 1
)

start "" /wait msiexec.exe /i "%MSI%"
exit /b %ERRORLEVEL%
