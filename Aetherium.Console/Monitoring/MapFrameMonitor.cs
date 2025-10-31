using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Aetherium.Model;

namespace Aetherium.Monitoring
{
    /// <summary>
    /// Monitors game state and broadcasts frame updates to subscribed WebSocket clients
    /// </summary>
    public class MapFrameMonitor : IDisposable
    {
        private static MapFrameMonitor? _instance;
        private static readonly object _lock = new object();

        private readonly MonitoringConfig _config;
        private readonly ConcurrentBag<WebSocket> _clients = new ConcurrentBag<WebSocket>();
        private HttpListener? _httpListener;
        private CancellationTokenSource? _cancellationTokenSource;
        private long _frameNumber = 0;
        private MapFrameLogger? _fileLogger;
        private bool _isRunning = false;

        public static MapFrameMonitor Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        if (_instance == null)
                        {
                            _instance = new MapFrameMonitor(new MonitoringConfig());
                        }
                    }
                }
                return _instance;
            }
        }

        public bool IsRunning => _isRunning;

        private MapFrameMonitor(MonitoringConfig config)
        {
            _config = config;
            
            if (_config.FileLogging.Enabled)
            {
                _fileLogger = new MapFrameLogger(_config.FileLogging.OutputPath);
            }
        }

        public static void Initialize(MonitoringConfig config)
        {
            lock (_lock)
            {
                _instance = new MapFrameMonitor(config);
            }
        }

        /// <summary>
        /// Starts the HTTP listener and begins accepting WebSocket connections
        /// </summary>
        public Task StartAsync()
        {
            if (!_config.Enabled || _isRunning)
                return Task.CompletedTask;

            _cancellationTokenSource = new CancellationTokenSource();
            
            try
            {
                _httpListener = new HttpListener();
                _httpListener.Prefixes.Add($"http://localhost:{_config.Port}/");
                _httpListener.Start();
                _isRunning = true;

                Console.WriteLine($"[Monitor] Listening on http://localhost:{_config.Port}/");
                Console.WriteLine($"[Monitor] WebSocket endpoint: ws://localhost:{_config.Port}/monitor");

                // Start accepting connections in background
                _ = Task.Run(() => AcceptConnectionsAsync(_cancellationTokenSource.Token));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Monitor] Failed to start: {ex.Message}");
                _isRunning = false;
            }

            return Task.CompletedTask;
        }

        /// <summary>
        /// Accepts incoming HTTP connections and upgrades to WebSocket
        /// </summary>
        private async Task AcceptConnectionsAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested && _httpListener != null && _httpListener.IsListening)
            {
                try
                {
                    var context = await _httpListener.GetContextAsync();
                    _ = Task.Run(async () => await HandleRequestAsync(context, cancellationToken), cancellationToken);
                }
                catch (HttpListenerException)
                {
                    // Listener was stopped
                    break;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[Monitor] Error accepting connection: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Handles an incoming HTTP request
        /// </summary>
        private async Task HandleRequestAsync(HttpListenerContext context, CancellationToken cancellationToken)
        {
            try
            {
                var request = context.Request;
                var response = context.Response;

                // Handle different endpoints
                if (request.Url?.AbsolutePath == "/monitor" && request.IsWebSocketRequest)
                {
                    await HandleWebSocketAsync(context, cancellationToken);
                }
                else if (request.Url?.AbsolutePath == "/health")
                {
                    await HandleHealthCheckAsync(response);
                }
                else if (request.Url?.AbsolutePath == "/config")
                {
                    await HandleConfigAsync(response);
                }
                else
                {
                    response.StatusCode = 404;
                    response.Close();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Monitor] Error handling request: {ex.Message}");
            }
        }

        /// <summary>
        /// Handles WebSocket upgrade and maintains connection
        /// </summary>
        private async Task HandleWebSocketAsync(HttpListenerContext context, CancellationToken cancellationToken)
        {
            WebSocket? webSocket = null;
            try
            {
                var wsContext = await context.AcceptWebSocketAsync(null);
                webSocket = wsContext.WebSocket;
                _clients.Add(webSocket);

                Console.WriteLine($"[Monitor] Client connected. Total clients: {_clients.Count(ws => ws.State == WebSocketState.Open)}");

                // Send welcome message
                var welcomeMsg = new MonitoringMessage
                {
                    Type = "welcome",
                    Data = null,
                    Error = null
                };
                await SendMessageAsync(webSocket, welcomeMsg, cancellationToken);

                // Keep connection alive and handle incoming messages
                var buffer = new byte[1024];
                while (webSocket.State == WebSocketState.Open && !cancellationToken.IsCancellationRequested)
                {
                    var result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken);
                    
                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", cancellationToken);
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Monitor] WebSocket error: {ex.Message}");
            }
            finally
            {
                if (webSocket != null)
                {
                    webSocket.Dispose();
                }
            }
        }

        /// <summary>
        /// Handles health check endpoint
        /// </summary>
        private async Task HandleHealthCheckAsync(HttpListenerResponse response)
        {
            var health = new
            {
                status = "healthy",
                connectedClients = _clients.Count(ws => ws.State == WebSocketState.Open),
                framesProcessed = _frameNumber
            };

            var json = JsonSerializer.Serialize(health);
            var buffer = Encoding.UTF8.GetBytes(json);

            response.ContentType = "application/json";
            response.ContentLength64 = buffer.Length;
            await response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
            response.Close();
        }

        /// <summary>
        /// Handles config endpoint
        /// </summary>
        private async Task HandleConfigAsync(HttpListenerResponse response)
        {
            var json = JsonSerializer.Serialize(_config);
            var buffer = Encoding.UTF8.GetBytes(json);

            response.ContentType = "application/json";
            response.ContentLength64 = buffer.Length;
            await response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
            response.Close();
        }

        /// <summary>
        /// Broadcasts a frame update to all connected clients
        /// </summary>
        public async Task BroadcastFrameAsync(PerceptionDto perception, AsciiMapData asciiMap)
        {
            if (!_isRunning)
                return;

            var frameUpdate = new MapFrameUpdate
            {
                Timestamp = DateTime.UtcNow,
                FrameNumber = Interlocked.Increment(ref _frameNumber),
                RawPerception = perception,
                AsciiMap = asciiMap
            };

            var message = new MonitoringMessage
            {
                Type = "frame",
                Data = frameUpdate
            };

            // Log to file if enabled
            if (_fileLogger != null && _config.FileLogging.Enabled)
            {
                await _fileLogger.LogFrameAsync(frameUpdate);
            }

            // Broadcast to all connected clients
            var tasks = new List<Task>();
            foreach (var client in _clients.Where(ws => ws.State == WebSocketState.Open))
            {
                tasks.Add(SendMessageAsync(client, message, CancellationToken.None));
            }

            if (tasks.Any())
            {
                try
                {
                    await Task.WhenAll(tasks);
                }
                catch
                {
                    // Individual send failures are handled in SendMessageAsync
                }
            }
        }

        /// <summary>
        /// Sends a message to a specific WebSocket client
        /// </summary>
        private async Task SendMessageAsync(WebSocket webSocket, MonitoringMessage message, CancellationToken cancellationToken)
        {
            try
            {
                if (webSocket.State != WebSocketState.Open)
                    return;

                var json = JsonSerializer.Serialize(message, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });
                var buffer = Encoding.UTF8.GetBytes(json);

                await webSocket.SendAsync(
                    new ArraySegment<byte>(buffer),
                    WebSocketMessageType.Text,
                    true,
                    cancellationToken);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Monitor] Error sending message: {ex.Message}");
            }
        }

        /// <summary>
        /// Stops the monitoring service and closes all connections
        /// </summary>
        public void Stop()
        {
            if (!_isRunning)
                return;

            _isRunning = false;
            _cancellationTokenSource?.Cancel();

            // Close all client connections
            foreach (var client in _clients.Where(ws => ws.State == WebSocketState.Open))
            {
                try
                {
                    client.CloseAsync(WebSocketCloseStatus.NormalClosure, "Server shutting down", CancellationToken.None).Wait(1000);
                }
                catch { }
            }

            _httpListener?.Stop();
            _httpListener?.Close();

            Console.WriteLine("[Monitor] Stopped");
        }

        public void Dispose()
        {
            Stop();
            _cancellationTokenSource?.Dispose();
            _httpListener?.Close();
            _fileLogger?.Dispose();
        }
    }
}


