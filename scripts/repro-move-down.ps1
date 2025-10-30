# Repro script: start server/client, send one Down Arrow, wait, then cleanup
# This script starts the processes directly (not via start-game-test.ps1) to avoid blocking on ReadKey

param(
    [int]$WaitBeforeKeysMs = 4000,
    [int]$WaitAfterKeysMs = 3000
)

$ErrorActionPreference = "Stop"

Write-Host "=== Repro: Move Down Bug ===" -ForegroundColor Cyan
Write-Host ""

# Clean up any existing processes
Write-Host "Cleaning up any existing game processes..." -ForegroundColor Yellow
Get-Process -Name "ConsoleGameServer" -ErrorAction SilentlyContinue | Stop-Process -Force
Get-Process -Name "ConsoleGameClient" -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Seconds 1

# Build the solution
Write-Host "Building solution..." -ForegroundColor Yellow
Push-Location "$PSScriptRoot/.."
dotnet build --nologo --verbosity quiet
if ($LASTEXITCODE -ne 0) {
    Write-Host "Build failed!" -ForegroundColor Red
    Pop-Location
    exit 1
}
Write-Host "Build successful!" -ForegroundColor Green
Write-Host ""

# Start the server in a new window
Write-Host "Starting server..." -ForegroundColor Yellow
$serverProcess = Start-Process powershell -ArgumentList "-NoExit", "-Command", "cd '$PWD\ConsoleGameServer'; Write-Host 'Starting server...' -ForegroundColor Green; dotnet run" -PassThru -WindowStyle Normal
Start-Sleep -Seconds 3

if ($serverProcess.HasExited) {
    Write-Host "Server failed to start!" -ForegroundColor Red
    Pop-Location
    exit 1
}
Write-Host "Server started (PID: $($serverProcess.Id))" -ForegroundColor Green

# Start the client in a new window
Write-Host "Starting client..." -ForegroundColor Yellow  
$clientProcess = Start-Process powershell -ArgumentList "-NoExit", "-Command", "cd '$PWD\ConsoleGame'; Write-Host 'Starting client...' -ForegroundColor Green; `$env:AUDIO_TEST='1'; dotnet run" -PassThru -WindowStyle Normal
Start-Sleep -Seconds 2

if ($clientProcess.HasExited) {
    Write-Host "Client failed to start!" -ForegroundColor Red
    Stop-Process -Id $serverProcess.Id -Force
    Pop-Location
    exit 1
}
Write-Host "Client started (PID: $($clientProcess.Id))" -ForegroundColor Green
Write-Host ""

# Wait for UI to stabilize
Write-Host "Waiting for UI to stabilize ($WaitBeforeKeysMs ms)..." -ForegroundColor Yellow
Start-Sleep -Milliseconds $WaitBeforeKeysMs

# Find and focus the client window, then send Down arrow
Write-Host "Sending Down arrow key..." -ForegroundColor Cyan

Add-Type -AssemblyName System.Windows.Forms
Add-Type @"
using System;
using System.Runtime.InteropServices;
public static class Win32 {
  [DllImport("user32.dll")] public static extern IntPtr FindWindow(string cls, string title);
  [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
}
"@

# Try to find the client window by title
$hWnd = [IntPtr]::Zero
$attempts = 0
while ($hWnd -eq [IntPtr]::Zero -and $attempts -lt 10) {
    $candidate = Get-Process | Where-Object { $_.MainWindowTitle -like '*ConsoleGameClient*' -or $_.Id -eq $clientProcess.Id } | Select-Object -First 1
    if ($candidate -and $candidate.MainWindowHandle -ne [IntPtr]::Zero) {
        $hWnd = $candidate.MainWindowHandle
    }
    if ($hWnd -eq [IntPtr]::Zero) {
        Start-Sleep -Milliseconds 200
        $attempts++
    }
}

if ($hWnd -eq [IntPtr]::Zero) {
    Write-Host "Could not find client window handle. Aborting repro." -ForegroundColor Red
    Stop-Process -Id $clientProcess.Id -Force -ErrorAction SilentlyContinue
    Stop-Process -Id $serverProcess.Id -Force -ErrorAction SilentlyContinue
    Pop-Location
    exit 1
}

[Win32]::SetForegroundWindow($hWnd) | Out-Null
Start-Sleep -Milliseconds 500
[System.Windows.Forms.SendKeys]::SendWait("{DOWN}")
Write-Host "Key sent!" -ForegroundColor Green

# Wait to observe the result
Write-Host "Waiting ($WaitAfterKeysMs ms) - check the client window for artifacts..." -ForegroundColor Yellow
Start-Sleep -Milliseconds $WaitAfterKeysMs

# Cleanup
Write-Host ""
Write-Host "Stopping processes..." -ForegroundColor Yellow
Stop-Process -Id $clientProcess.Id -Force -ErrorAction SilentlyContinue
Stop-Process -Id $serverProcess.Id -Force -ErrorAction SilentlyContinue
Get-Process -Name "ConsoleGameServer","ConsoleGameClient" -ErrorAction SilentlyContinue | Stop-Process -Force

Pop-Location
Write-Host "Done!" -ForegroundColor Green
