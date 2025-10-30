# Minimal monitor to validate map frames
param(
    [string]$ServerUrl = "ws://localhost:5001/monitor",
    [int]$MaxFrames = 3
)

$ws = New-Object System.Net.WebSockets.ClientWebSocket
$ct = New-Object System.Threading.CancellationToken

try {
    $uri = [System.Uri]::new($ServerUrl)
    $ws.ConnectAsync($uri, $ct).Wait()
    if ($ws.State -ne [System.Net.WebSockets.WebSocketState]::Open) {
        Write-Host "Failed to connect to monitor" -ForegroundColor Red
        exit 1
    }

    $buffer = New-Object Byte[] 65536
    $frames = 0
    while ($ws.State -eq [System.Net.WebSockets.WebSocketState]::Open -and $frames -lt $MaxFrames) {
        $segment = New-Object System.ArraySegment[Byte] -ArgumentList @(,$buffer)
        $ws.ReceiveAsync($segment, $ct).Wait()
        $json = [System.Text.Encoding]::UTF8.GetString($buffer, 0, $segment.Count)
        try { $obj = $json | ConvertFrom-Json } catch { continue }
        if ($obj.type -eq 'frame') {
            $frames++
            $hasMap = $obj.data -and $obj.data.asciiMap -and $obj.data.asciiMap.tiles.Count -gt 0
            Write-Host ("Frame {0}: tiles={1}" -f $obj.data.frameNumber, ($hasMap)) -ForegroundColor Green
        }
    }
}
finally {
    if ($ws.State -eq [System.Net.WebSockets.WebSocketState]::Open) {
        $ws.CloseAsync([System.Net.WebSockets.WebSocketCloseStatus]::NormalClosure, "done", $ct).Wait()
    }
    $ws.Dispose()
}


