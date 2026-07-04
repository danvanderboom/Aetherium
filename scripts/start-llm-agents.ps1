# Quickstart script for LLM-driven agents
# This script helps set up and run LLM agents with LM Studio (phi-4)

param(
    [switch]$SkipLMStudioCheck,
    [switch]$HeuristicOnly,
    [int]$AgentCount = 2
)

$ErrorActionPreference = "Stop"

Write-Host "=== LLM Agent Quickstart ===" -ForegroundColor Cyan
Write-Host ""

# Check if LM Studio is available
if (-not $SkipLMStudioCheck -and -not $HeuristicOnly) {
    Write-Host "Checking LM Studio availability..." -ForegroundColor Yellow
    try {
        $response = Invoke-WebRequest -Uri "http://localhost:1234/v1/models" -UseBasicParsing -TimeoutSec 2 -ErrorAction Stop
        Write-Host "✓ LM Studio is running" -ForegroundColor Green
        
        # Check if phi-4 model is available
        $models = $response.Content | ConvertFrom-Json
        $phi4Available = $models.data | Where-Object { $_.id -like "*phi*4*" -or $_.id -like "*Phi-4*" }
        if ($phi4Available) {
            Write-Host "✓ phi-4 model is available" -ForegroundColor Green
        } else {
            Write-Host "⚠ Warning: phi-4 model not found. Available models:" -ForegroundColor Yellow
            $models.data | ForEach-Object { Write-Host "  - $($_.id)" -ForegroundColor Gray }
            Write-Host ""
            $continue = Read-Host "Continue anyway? (y/n)"
            if ($continue -ne "y") {
                Write-Host "Exiting. Please load phi-4 model in LM Studio and try again." -ForegroundColor Red
                exit 1
            }
        }
    } catch {
        Write-Host "✗ LM Studio is not available at http://localhost:1234" -ForegroundColor Red
        Write-Host ""
        Write-Host "To use LLM agents, you need to:" -ForegroundColor Yellow
        Write-Host "  1. Install and start LM Studio (https://lmstudio.ai/)" -ForegroundColor White
        Write-Host "  2. Download and load the phi-4 model" -ForegroundColor White
        Write-Host "  3. Start the local server (default: http://localhost:1234)" -ForegroundColor White
        Write-Host ""
        Write-Host "Alternatively, you can:" -ForegroundColor Yellow
        Write-Host "  - Use heuristic agents (no LLM required): Run with -HeuristicOnly" -ForegroundColor White
        Write-Host "  - Skip LM Studio check: Run with -SkipLMStudioCheck" -ForegroundColor White
        Write-Host ""
        $continue = Read-Host "Continue with heuristic agents? (y/n)"
        if ($continue -ne "y") {
            exit 1
        }
        $HeuristicOnly = $true
    }
    Write-Host ""
}

# Set environment variables
Write-Host "Setting environment variables..." -ForegroundColor Yellow

if ($HeuristicOnly) {
    $env:AGENT_LLM_ENABLED = "0"
    Write-Host "  AGENT_LLM_ENABLED = 0 (heuristic mode)" -ForegroundColor Gray
} else {
    $env:AGENT_LLM_ENABLED = "1"
    $env:OPENAI_API_BASE = "http://localhost:1234/v1"
    $env:OPENAI_API_KEY = "lm-studio"
    $env:AGENT_MODEL = "phi-4"
    Write-Host "  AGENT_LLM_ENABLED = 1" -ForegroundColor Gray
    Write-Host "  OPENAI_API_BASE = http://localhost:1234/v1" -ForegroundColor Gray
    Write-Host "  AGENT_MODEL = phi-4" -ForegroundColor Gray
}

$env:AGENT_DEBUG = "1"  # Enable debug output
Write-Host "  AGENT_DEBUG = 1" -ForegroundColor Gray
Write-Host ""

# Build the solution
Write-Host "Building solution..." -ForegroundColor Yellow
dotnet build --nologo --verbosity quiet
if ($LASTEXITCODE -ne 0) {
    Write-Host "✗ Build failed!" -ForegroundColor Red
    exit 1
}
Write-Host "✓ Build successful" -ForegroundColor Green
Write-Host ""

# Start the server
Write-Host "Starting game server..." -ForegroundColor Yellow
$serverProcess = Start-Process powershell -ArgumentList "-NoExit", "-Command", @"
cd '$PWD\Aetherium.Server'
Write-Host 'Game Server (LLM Agents Enabled)' -ForegroundColor Cyan
Write-Host ''
`$env:AGENT_LLM_ENABLED='$env:AGENT_LLM_ENABLED'
`$env:OPENAI_API_BASE='$env:OPENAI_API_BASE'
`$env:OPENAI_API_KEY='$env:OPENAI_API_KEY'
`$env:AGENT_MODEL='$env:AGENT_MODEL'
`$env:AGENT_DEBUG='$env:AGENT_DEBUG'
dotnet run
"@ -PassThru -WindowStyle Normal

Start-Sleep -Seconds 5

if ($serverProcess.HasExited) {
    Write-Host "✗ Server failed to start!" -ForegroundColor Red
    exit 1
}

Write-Host "✓ Server started (PID: $($serverProcess.Id))" -ForegroundColor Green
Write-Host ""

# Save PIDs for cleanup
$pidFile = Join-Path $PSScriptRoot ".llm-agent-pids.json"
@{
    ServerPID = $serverProcess.Id
    StartedAt = (Get-Date)
    AgentMode = if ($HeuristicOnly) { "heuristic" } else { "llm" }
} | ConvertTo-Json | Set-Content -Path $pidFile -Encoding UTF8

# Instructions
Write-Host "=== Agent System Ready ===" -ForegroundColor Cyan
Write-Host ""
Write-Host "Next steps:" -ForegroundColor Yellow
Write-Host "  1. Wait for the server to fully start (check server window)" -ForegroundColor White
Write-Host "  2. Start the game client (in a new terminal):" -ForegroundColor White
Write-Host "     cd Aetherium.Console" -ForegroundColor Gray
Write-Host "     dotnet run" -ForegroundColor Gray
Write-Host ""
Write-Host "  3. In another terminal, use aetherctl to attach and run agents:" -ForegroundColor White
Write-Host "     # List active sessions" -ForegroundColor Gray
Write-Host "     aetherctl session list" -ForegroundColor Gray
Write-Host ""
Write-Host "     # Attach agent to session (replace <sessionId> with actual ID)" -ForegroundColor Gray
Write-Host "     aetherctl agent attach <sessionId> --agent agent-1 --runner runner-1" -ForegroundColor Gray
Write-Host ""
Write-Host "     # ...or attach directly to a grain-hosted map (no human session needed)" -ForegroundColor Gray
Write-Host "     aetherctl agent attach-world <worldId> <mapId> --agent agent-1 --runner runner-1" -ForegroundColor Gray
Write-Host ""
Write-Host "     # Run the agent loop" -ForegroundColor Gray
Write-Host "     aetherctl agent run runner-1 --max-steps 50 --delay 200" -ForegroundColor Gray
Write-Host ""
Write-Host "     # Check agent status" -ForegroundColor Gray
Write-Host "     aetherctl agent status runner-1" -ForegroundColor Gray
Write-Host ""
Write-Host "  4. LLM vs heuristic policy and debug output are controlled by environment" -ForegroundColor White
Write-Host "     variables read at server startup (this script sets them for the server):" -ForegroundColor White
Write-Host "     AGENT_LLM_ENABLED=1  -> LLM policy;  AGENT_LLM_ENABLED=0 -> heuristic" -ForegroundColor Gray
Write-Host "     AGENT_DEBUG=1        -> verbose agent decision logging" -ForegroundColor Gray
Write-Host "     Change them and restart the server window to switch modes." -ForegroundColor Gray
Write-Host ""
Write-Host "Press any key to stop the server and clean up..." -ForegroundColor Cyan

# Cleanup handler.
# $serverProcess is the powershell wrapper spawned by Start-Process, not the server itself;
# `taskkill /T` kills its whole child tree (the dotnet/server process) so nothing is orphaned.
# Store the PID/paths in script scope — a [ConsoleCancelEventHandler] delegate runs in this
# runspace, so it reads script-scoped variables directly ($using: is only valid for remote/job
# script blocks and would not resolve here).
$script:cleanupDone = $false
$script:serverPid = $serverProcess.Id
$script:pidFilePath = $pidFile
$script:cancelHandler = [ConsoleCancelEventHandler]{
    param($sender, $eventArgs)
    $eventArgs.Cancel = $true
    Write-Host "`nCtrl+C detected, cleaning up..." -ForegroundColor Yellow
    try {
        taskkill /PID $script:serverPid /T /F 2>$null | Out-Null
        Get-Process -Name "Aetherium.Server" -ErrorAction SilentlyContinue | Stop-Process -Force
    } finally {
        Remove-Item -Path $script:pidFilePath -Force -ErrorAction SilentlyContinue
        $script:cleanupDone = $true
    }
    exit
}
[Console]::add_CancelKeyPress($script:cancelHandler)

try {
    $null = $host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
} catch {
    # User cancelled with Ctrl+C
} finally {
    Write-Host ""
    Write-Host "Stopping server..." -ForegroundColor Yellow
    taskkill /PID $script:serverPid /T /F 2>$null | Out-Null
    Get-Process -Name "Aetherium.Server" -ErrorAction SilentlyContinue | Stop-Process -Force
    Remove-Item -Path $pidFile -Force -ErrorAction SilentlyContinue
    [Console]::remove_CancelKeyPress($script:cancelHandler)
    Write-Host "✓ Done!" -ForegroundColor Green
}


