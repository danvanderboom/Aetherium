param(
    [int]$TimeoutSeconds = 20
)

$ErrorActionPreference = "Stop"

Write-Host "=== Client UI Self-Test ===" -ForegroundColor Cyan

# Clean up any existing processes
Get-Process -Name "ConsoleGameServer","ConsoleGameClient" -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Seconds 1

# Build solution
Write-Host "Building..." -ForegroundColor Yellow
pushd "$PSScriptRoot/.."
dotnet build --nologo --verbosity quiet
if ($LASTEXITCODE -ne 0) { popd; throw "Build failed" }
Write-Host "Build OK" -ForegroundColor Green

# Start server
Write-Host "Starting server..." -ForegroundColor Yellow
$server = Start-Process powershell -ArgumentList "-NoExit","-Command","cd '$PWD\ConsoleGameServer'; `$env:AUDIO_TEST='1'; dotnet run" -PassThru -WindowStyle Normal
Start-Sleep -Seconds 3

# Run client in self-test mode
Write-Host "Running client self-test..." -ForegroundColor Yellow
$client = Start-Process powershell -ArgumentList "-NoExit","-Command","cd '$PWD\ConsoleGame'; dotnet run -- --ui-selftest" -PassThru -WindowStyle Normal

Write-Host "Waiting up to $TimeoutSeconds seconds for completion..." -ForegroundColor Yellow
$sw = [Diagnostics.Stopwatch]::StartNew()
while (-not $client.HasExited -and $sw.Elapsed.TotalSeconds -lt $TimeoutSeconds) { Start-Sleep -Milliseconds 200 }

if (-not $client.HasExited) {
  Write-Host "Client did not finish in time; stopping..." -ForegroundColor Red
  Stop-Process -Id $client.Id -Force -ErrorAction SilentlyContinue
}

$code = if ($client.HasExited) { $client.ExitCode } else { 1 }

Write-Host "Stopping server..." -ForegroundColor Yellow
Stop-Process -Id $server.Id -Force -ErrorAction SilentlyContinue
Get-Process -Name "ConsoleGameServer","ConsoleGameClient" -ErrorAction SilentlyContinue | Stop-Process -Force

popd

if ($code -eq 0) {
  Write-Host "UI Self-Test: PASSED" -ForegroundColor Green
  exit 0
} else {
  Write-Host "UI Self-Test: FAILED (see .ui-test/before.txt and after.txt)" -ForegroundColor Red
  exit 1
}


