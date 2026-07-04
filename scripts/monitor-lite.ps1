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
        # A single message may span multiple WebSocket frames; accumulate until EndOfMessage.
        $ms = New-Object System.IO.MemoryStream
        $result = $null
        do {
            $segment = New-Object System.ArraySegment[Byte] -ArgumentList @(,$buffer)
            $recv = $ws.ReceiveAsync($segment, $ct)
            $recv.Wait()
            $result = $recv.Result
            if ($result.MessageType -eq [System.Net.WebSockets.WebSocketMessageType]::Close) { break }
            # Only the bytes actually received this call are valid - decoding the whole 64 KB
            # buffer would append trailing NULs and the JSON parse would always fail.
            $ms.Write($buffer, 0, $result.Count)
        } while (-not $result.EndOfMessage)

        if ($result.MessageType -eq [System.Net.WebSockets.WebSocketMessageType]::Close) { $ms.Dispose(); break }

        $json = [System.Text.Encoding]::UTF8.GetString($ms.ToArray())
        $ms.Dispose()
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


