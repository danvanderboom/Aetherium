param(
    [int]$TimeoutSeconds = 20
)

$ErrorActionPreference = "Stop"

Write-Host "=== Client UI Self-Test ===" -ForegroundColor Cyan

# Clean up any existing processes
Get-Process -Name "Aetherium.Server","Aetherium.Console" -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Seconds 1

# Build solution
Write-Host "Building..." -ForegroundColor Yellow
pushd "$PSScriptRoot/.."
dotnet build --nologo --verbosity quiet
if ($LASTEXITCODE -ne 0) { popd; throw "Build failed" }
Write-Host "Build OK" -ForegroundColor Green

# Determine artifacts directory (absolute path in project root)
$artifactsDir = Join-Path $PWD ".ui-test"
Write-Host "Artifacts will be written to: $artifactsDir" -ForegroundColor Cyan

# Start server
Write-Host "Starting server..." -ForegroundColor Yellow
$server = Start-Process powershell -ArgumentList "-NoExit","-Command","cd '$PWD\Aetherium.Server'; `$env:UI_SELFTEST_MODE='1'; dotnet run" -PassThru -WindowStyle Normal

# Wait for server to be ready by checking if port 5000 is listening
Write-Host "Waiting for server to be ready..." -ForegroundColor Yellow
$maxWait = 30 # seconds
$waited = 0
$serverReady = $false
while ($waited -lt $maxWait -and -not $serverReady) {
    try {
        $connection = Test-NetConnection -ComputerName localhost -Port 5000 -InformationLevel Quiet -WarningAction SilentlyContinue
        if ($connection) {
            $serverReady = $true
            Write-Host "Server is ready on port 5000" -ForegroundColor Green
        }
    } catch {
        # Ignore connection errors during startup
    }
    if (-not $serverReady) {
        Start-Sleep -Seconds 1
        $waited++
        Write-Host "." -NoNewline -ForegroundColor Yellow
    }
}
Write-Host ""

if (-not $serverReady) {
    Write-Host "ERROR: Server failed to start within $maxWait seconds" -ForegroundColor Red
    Stop-Process -Id $server.Id -Force -ErrorAction SilentlyContinue
    popd
    exit 1
}

# Run client in self-test mode, passing artifacts directory as absolute path
Write-Host "Running client self-test..." -ForegroundColor Yellow
$client = Start-Process powershell -ArgumentList "-Command","cd '$PWD\Aetherium.Console'; `$env:UI_SELFTEST_MODE='1'; dotnet run -- --ui-selftest $artifactsDir" -PassThru -WindowStyle Normal

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
Get-Process -Name "Aetherium.Server","Aetherium.Console" -ErrorAction SilentlyContinue | Stop-Process -Force

popd

if ($code -eq 0) {
  Write-Host "UI Self-Test: PASSED" -ForegroundColor Green
  exit 0
} else {
  Write-Host "UI Self-Test: FAILED" -ForegroundColor Red
  Write-Host "See artifacts in: $artifactsDir" -ForegroundColor Yellow
  if (Test-Path "$artifactsDir\before.txt") { Write-Host "  - before.txt exists" }
  if (Test-Path "$artifactsDir\after.txt") { Write-Host "  - after.txt exists" }
  if (Test-Path "$artifactsDir\result.txt") { Write-Host "  - result.txt exists" }
  exit 1
}



