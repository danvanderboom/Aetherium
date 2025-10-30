# Script to start the game for testing UI
# This will start both server and client, then wait for user to inspect the UI

param(
    [int]$TimeoutSeconds = 0
)

$ErrorActionPreference = "Stop"

Write-Host "=== Console Game UI Test Script ===" -ForegroundColor Cyan
Write-Host ""

# Clean up any existing processes
Write-Host "Cleaning up any existing game processes..." -ForegroundColor Yellow
Get-Process -Name "ConsoleGameServer" -ErrorAction SilentlyContinue | Stop-Process -Force
Get-Process -Name "ConsoleGameClient" -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Seconds 1

# Build the solution
Write-Host "Building solution..." -ForegroundColor Yellow
dotnet build --nologo --verbosity quiet
if ($LASTEXITCODE -ne 0) {
    Write-Host "Build failed!" -ForegroundColor Red
    exit 1
}
Write-Host "Build successful!" -ForegroundColor Green
Write-Host ""

# Start the server in a new window
Write-Host "Starting server..." -ForegroundColor Yellow
$serverProcess = Start-Process powershell -ArgumentList "-NoExit", "-Command", "cd '$PWD\ConsoleGameServer'; Write-Host 'Starting server...' -ForegroundColor Green; dotnet run" -PassThru -WindowStyle Normal
Start-Sleep -Seconds 3

# Check if server started
if ($serverProcess.HasExited) {
    Write-Host "Server failed to start!" -ForegroundColor Red
    exit 1
}
Write-Host "Server started (PID: $($serverProcess.Id))" -ForegroundColor Green
Write-Host ""

# Start the client in a new window
Write-Host "Starting client..." -ForegroundColor Yellow  
$clientProcess = Start-Process powershell -ArgumentList "-NoExit", "-Command", "cd '$PWD\ConsoleGame'; Write-Host 'Starting client...' -ForegroundColor Green; `$env:AUDIO_TEST='1'; dotnet run" -PassThru -WindowStyle Normal
Start-Sleep -Seconds 2

# Check if client started
if ($clientProcess.HasExited) {
    Write-Host "Client failed to start!" -ForegroundColor Red
    Write-Host "Stopping server..." -ForegroundColor Yellow
    Stop-Process -Id $serverProcess.Id -Force
    exit 1
}
Write-Host "Client started (PID: $($clientProcess.Id))" -ForegroundColor Green
Write-Host ""

Write-Host "=== Game Running ===" -ForegroundColor Cyan
Write-Host "Server PID: $($serverProcess.Id)" -ForegroundColor White
Write-Host "Client PID: $($clientProcess.Id)" -ForegroundColor White
Write-Host ""
Write-Host "The client window should show the game UI." -ForegroundColor Yellow
Write-Host "Look for alignment issues in the right sidebar (COMPASS, INVENTORY, HELP panels)." -ForegroundColor Yellow
Write-Host ""
Write-Host "Press any key to stop the game and clean up (or Ctrl+C to exit)..." -ForegroundColor Cyan

# Persist PIDs for out-of-band cleanup
$pidFile = Join-Path $PSScriptRoot ".game-run-pids.json"
@{
    ServerPID = $serverProcess.Id
    ClientPID = $clientProcess.Id
    StartedAt = (Get-Date)
} | ConvertTo-Json | Set-Content -Path $pidFile -Encoding UTF8

# Set up cleanup for Ctrl+C (Console cancel event)
$script:cleanupDone = $false
$script:cancelHandler = [ConsoleCancelEventHandler]{
    param($sender, $eventArgs)
    $eventArgs.Cancel = $true
    Write-Host "`nCtrl+C detected, cleaning up..." -ForegroundColor Yellow
    try {
        Stop-Process -Id $using:clientProcess.Id -Force -ErrorAction SilentlyContinue
        Stop-Process -Id $using:serverProcess.Id -Force -ErrorAction SilentlyContinue
        Get-Process -Name "ConsoleGameServer","ConsoleGameClient" -ErrorAction SilentlyContinue | Stop-Process -Force
    } finally {
        Remove-Item -Path $using:pidFile -Force -ErrorAction SilentlyContinue
        $script:cleanupDone = $true
    }
    exit
}
[Console]::add_CancelKeyPress($script:cancelHandler)

try {
    if ($TimeoutSeconds -gt 0) {
        $sw = [System.Diagnostics.Stopwatch]::StartNew()
        while ($sw.Elapsed.TotalSeconds -lt $TimeoutSeconds -and -not [Console]::KeyAvailable) {
            Start-Sleep -Milliseconds 200
        }
        if ([Console]::KeyAvailable) {
            [void][Console]::ReadKey($true)
        }
    } else {
        $null = $host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
    }
}
catch {
    # User cancelled with Ctrl+C
}
finally {
    # Clean up
    Write-Host ""
    Write-Host "Stopping processes..." -ForegroundColor Yellow
    Stop-Process -Id $clientProcess.Id -Force -ErrorAction SilentlyContinue
    Stop-Process -Id $serverProcess.Id -Force -ErrorAction SilentlyContinue
    
    # Extra cleanup to make sure nothing is left running
    Get-Process -Name "ConsoleGameServer" -ErrorAction SilentlyContinue | Stop-Process -Force
    Get-Process -Name "ConsoleGameClient" -ErrorAction SilentlyContinue | Stop-Process -Force
    
    $script:cleanupDone = $true
    Remove-Item -Path $pidFile -Force -ErrorAction SilentlyContinue
    [Console]::remove_CancelKeyPress($script:cancelHandler)
    Write-Host "Done!" -ForegroundColor Green
}

