# Repro script: start server/client, send one Down Arrow, wait, then cleanup

param(
    [int]$WaitBeforeKeysMs = 3000,
    [int]$WaitAfterKeysMs = 2000
)

$ErrorActionPreference = "Stop"

Write-Host "Starting game..." -ForegroundColor Cyan
& "$PSScriptRoot/../start-game-test.ps1" -TimeoutSeconds 0 | Out-Null

Start-Sleep -Milliseconds $WaitBeforeKeysMs

Add-Type -AssemblyName System.Windows.Forms
Add-Type @"
using System;
using System.Runtime.InteropServices;
public static class Win32 {
  [DllImport("user32.dll")] public static extern IntPtr FindWindow(string cls, string title);
  [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
}
"@

# Try to find the client window by title set in Program.cs
$hWnd = [Win32]::FindWindow("ConsoleWindowClass", "ConsoleGameClient")
if ($hWnd -eq [IntPtr]::Zero) {
  # fallback: try any window with title containing 'ConsoleGameClient'
  $candidate = Get-Process | Where-Object { $_.MainWindowTitle -like '*ConsoleGameClient*' } | Select-Object -First 1
  if ($candidate) { $hWnd = $candidate.MainWindowHandle }
}

if ($hWnd -eq [IntPtr]::Zero) {
  Write-Host "Could not find client window. Aborting repro." -ForegroundColor Red
  & "$PSScriptRoot/../stop-game.ps1" -All | Out-Null
  exit 1
}

[Win32]::SetForegroundWindow($hWnd) | Out-Null
[System.Windows.Forms.SendKeys]::SendWait("{DOWN}")

Start-Sleep -Milliseconds $WaitAfterKeysMs

Write-Host "Stopping game..." -ForegroundColor Yellow
& "$PSScriptRoot/../stop-game.ps1" -All | Out-Null
Write-Host "Done." -ForegroundColor Green


