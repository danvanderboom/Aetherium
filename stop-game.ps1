# Stop script to clean up running ConsoleGame server/client started by start-game-test.ps1

param(
    [switch]$All
)

$ErrorActionPreference = "SilentlyContinue"

$pidFile = Join-Path $PSScriptRoot ".game-run-pids.json"
if (Test-Path $pidFile) {
    try {
        $info = Get-Content -Raw -Path $pidFile | ConvertFrom-Json
        if ($info.ServerPID) { Stop-Process -Id $info.ServerPID -Force }
        if ($info.ClientPID) { Stop-Process -Id $info.ClientPID -Force }
    } catch {}
    Remove-Item -Path $pidFile -Force -ErrorAction SilentlyContinue
}

if ($All) {
    Get-Process -Name "ConsoleGameServer","ConsoleGameClient" | Stop-Process -Force
}

Write-Host "Cleanup complete." -ForegroundColor Green


