# PowerShell Game Monitor Client
# Connects to the console game monitoring service and displays frame updates

param(
    [string]$ServerUrl = "ws://localhost:5001/monitor",
    [switch]$SaveToFile,
    [string]$OutputPath = "./monitor-output",
    [switch]$DisplayAsciiMap,
    [switch]$DisplayJson,
    [switch]$Verbose
)

# Create output directory if saving to file
if ($SaveToFile -and -not (Test-Path $OutputPath)) {
    New-Item -ItemType Directory -Path $OutputPath | Out-Null
    Write-Host "Created output directory: $OutputPath" -ForegroundColor Green
}

Write-Host "=== Console Game Monitor ===" -ForegroundColor Cyan
Write-Host "Connecting to: $ServerUrl" -ForegroundColor Yellow
Write-Host ""

# Create WebSocket client
$ws = New-Object System.Net.WebSockets.ClientWebSocket
$ct = New-Object System.Threading.CancellationToken

try {
    # Connect to monitoring service
    $uri = [System.Uri]::new($ServerUrl)
    $connectTask = $ws.ConnectAsync($uri, $ct)
    $connectTask.Wait()

    if ($ws.State -eq [System.Net.WebSockets.WebSocketState]::Open) {
        Write-Host "Connected successfully!" -ForegroundColor Green
        Write-Host ""
        Write-Host "Waiting for frame updates... (Press Ctrl+C to stop)" -ForegroundColor Yellow
        Write-Host "============================================================" -ForegroundColor DarkGray
        Write-Host ""
    }

    # Receive loop
    $buffer = New-Object Byte[] 65536
    $frameCount = 0

    while ($ws.State -eq [System.Net.WebSockets.WebSocketState]::Open) {
        $segment = New-Object System.ArraySegment[Byte] -ArgumentList @(,$buffer)
        $receiveTask = $ws.ReceiveAsync($segment, $ct)
        $receiveTask.Wait()

        $result = $receiveTask.Result

        if ($result.MessageType -eq [System.Net.WebSockets.WebSocketMessageType]::Close) {
            Write-Host "Server closed connection" -ForegroundColor Red
            break
        }

        if ($result.MessageType -eq [System.Net.WebSockets.WebSocketMessageType]::Text) {
            $message = [System.Text.Encoding]::UTF8.GetString($buffer, 0, $result.Count)
            
            # Parse JSON message
            $json = $message | ConvertFrom-Json

            if ($json.type -eq "welcome") {
                Write-Host "Received welcome message from server" -ForegroundColor Green
                continue
            }

            if ($json.type -eq "frame" -and $json.data) {
                $frameCount++
                $frame = $json.data

                # Display frame info
                Write-Host "────────────────────────────────────────────────────────────" -ForegroundColor DarkGray
                Write-Host "Frame #$($frame.frameNumber) - $($frame.timestamp)" -ForegroundColor Cyan
                
                if ($frame.rawPerception) {
                    $perception = $frame.rawPerception
                    Write-Host "  Player Location: ($($perception.playerLocation.x), $($perception.playerLocation.y), $($perception.playerLocation.z))" -ForegroundColor White
                    Write-Host "  Player Heading: $($perception.playerHeading)" -ForegroundColor White
                    Write-Host "  Visible Tiles: $($perception.visuals.Count)" -ForegroundColor White
                    
                    if ($perception.inventory) {
                        Write-Host "  Inventory: $($perception.inventory.items.Count)/$($perception.inventory.capacity)" -ForegroundColor White
                        if ($perception.inventory.items.Count -gt 0) {
                            $itemNames = $perception.inventory.items | ForEach-Object { $_.label }
                            Write-Host "    Items: $($itemNames -join ', ')" -ForegroundColor Gray
                        }
                    }
                }

                # Display ASCII map
                if ($DisplayAsciiMap -and $frame.asciiMap) {
                    Write-Host ""
                    Write-Host "  Map ($($frame.asciiMap.width)x$($frame.asciiMap.height)):" -ForegroundColor Yellow
                    
                    # Top border
                    Write-Host "  ┌$('─' * ($frame.asciiMap.width * 2))┐" -ForegroundColor DarkGray
                    
                    # Map rows
                    foreach ($row in $frame.asciiMap.tiles) {
                        $rowStr = "  │"
                        foreach ($tile in $row) {
                            $rowStr += $tile
                        }
                        $rowStr += "│"
                        Write-Host $rowStr -ForegroundColor White
                    }
                    
                    # Bottom border
                    Write-Host "  └$('─' * ($frame.asciiMap.width * 2))┘" -ForegroundColor DarkGray
                }

                # Display full JSON if requested
                if ($DisplayJson) {
                    Write-Host ""
                    Write-Host "  Raw JSON:" -ForegroundColor Magenta
                    Write-Host "  $message" -ForegroundColor DarkGray
                }

                # Save to file if requested
                if ($SaveToFile) {
                    $timestamp = Get-Date -Format "yyyyMMdd_HHmmss_fff"
                    $filename = Join-Path $OutputPath "frame_$($frame.frameNumber)_$timestamp.json"
                    $message | Out-File -FilePath $filename -Encoding UTF8
                    
                    if ($Verbose) {
                        Write-Host "  Saved to: $filename" -ForegroundColor Green
                    }
                }

                Write-Host ""
            }
        }
    }

} catch {
    Write-Host "Error: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host $_.Exception.StackTrace -ForegroundColor DarkRed
} finally {
    if ($ws) {
        if ($ws.State -eq [System.Net.WebSockets.WebSocketState]::Open) {
            $closeTask = $ws.CloseAsync(
                [System.Net.WebSockets.WebSocketCloseStatus]::NormalClosure,
                "Client disconnecting",
                $ct
            )
            $closeTask.Wait(1000) | Out-Null
        }
        $ws.Dispose()
    }

    Write-Host ""
    Write-Host "============================================================" -ForegroundColor DarkGray
    Write-Host "Disconnected. Total frames received: $frameCount" -ForegroundColor Yellow
}

# Examples:
# 
# Basic monitoring (just stats):
#   .\monitor-game.ps1
#
# Display ASCII maps:
#   .\monitor-game.ps1 -DisplayAsciiMap
#
# Save frames to files:
#   .\monitor-game.ps1 -SaveToFile -OutputPath "./my-monitor-logs"
#
# Full verbose monitoring:
#   .\monitor-game.ps1 -DisplayAsciiMap -DisplayJson -SaveToFile -Verbose
#
# Connect to different port:
#   .\monitor-game.ps1 -ServerUrl "ws://localhost:5002/monitor" -DisplayAsciiMap

