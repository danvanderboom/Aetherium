# Repro script: start server/client, prompt user to press Down, observe for artifacts
# Simplified version - user presses Down key manually in the client window

param(
    [int]$ObservationSeconds = 5
)

$ErrorActionPreference = "Stop"

Write-Host "=== Repro: Move Down Bug ===" -ForegroundColor Cyan
Write-Host ""

# Clean up any existing processes
Write-Host "Cleaning up any existing game processes..." -ForegroundColor Yellow
Get-Process -Name "Aetherium.Server" -ErrorAction SilentlyContinue | Stop-Process -Force
Get-Process -Name "Aetherium.Console" -ErrorAction SilentlyContinue | Stop-Process -Force
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
$serverProcess = Start-Process powershell -ArgumentList "-NoExit", "-Command", "cd '$PWD\Aetherium.Server'; Write-Host 'Starting server...' -ForegroundColor Green; dotnet run" -PassThru -WindowStyle Normal
Start-Sleep -Seconds 3

if ($serverProcess.HasExited) {
    Write-Host "Server failed to start!" -ForegroundColor Red
    Pop-Location
    exit 1
}
Write-Host "Server started (PID: $($serverProcess.Id))" -ForegroundColor Green

# Start the client in a new window
Write-Host "Starting client..." -ForegroundColor Yellow  
$clientProcess = Start-Process powershell -ArgumentList "-NoExit", "-Command", "cd '$PWD\Aetherium.Console'; Write-Host 'Starting client...' -ForegroundColor Green; `$env:AUDIO_TEST='1'; dotnet run" -PassThru -WindowStyle Normal
Start-Sleep -Seconds 3

if ($clientProcess.HasExited) {
    Write-Host "Client failed to start!" -ForegroundColor Red
    Stop-Process -Id $serverProcess.Id -Force
    Pop-Location
    exit 1
}
Write-Host "Client started (PID: $($clientProcess.Id))" -ForegroundColor Green
Write-Host ""

Write-Host "=== INSTRUCTIONS ===" -ForegroundColor Cyan
Write-Host "1. Switch to the CLIENT window" -ForegroundColor Yellow
Write-Host "2. Press the DOWN ARROW key ONCE" -ForegroundColor Yellow
Write-Host "3. Observe if there are any visual artifacts (overlapping text, ghosting)" -ForegroundColor Yellow
Write-Host ""
Write-Host "Waiting $ObservationSeconds seconds for observation..." -ForegroundColor Cyan
Write-Host "(Press Ctrl+C to stop earlier)" -ForegroundColor DarkGray
Write-Host ""

Start-Sleep -Seconds $ObservationSeconds

# Cleanup
Write-Host ""
Write-Host "Stopping processes..." -ForegroundColor Yellow
Stop-Process -Id $clientProcess.Id -Force -ErrorAction SilentlyContinue
Stop-Process -Id $serverProcess.Id -Force -ErrorAction SilentlyContinue
Get-Process -Name "Aetherium.Server","Aetherium.Console" -ErrorAction SilentlyContinue | Stop-Process -Force

Pop-Location
Write-Host "Done!" -ForegroundColor Green

