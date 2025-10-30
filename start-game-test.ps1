# Script to start the game for testing UI
# This will start both server and client, then wait for user to inspect the UI

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
Write-Host "Press any key to stop the game and clean up..." -ForegroundColor Cyan
$null = $host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")

# Clean up
Write-Host ""
Write-Host "Stopping processes..." -ForegroundColor Yellow
Stop-Process -Id $clientProcess.Id -Force -ErrorAction SilentlyContinue
Stop-Process -Id $serverProcess.Id -Force -ErrorAction SilentlyContinue

Write-Host "Done!" -ForegroundColor Green

