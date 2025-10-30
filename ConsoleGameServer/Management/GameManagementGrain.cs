using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ConsoleGame.Components;
using ConsoleGameModel;
using ConsoleGameServer.MultiWorld;
using Microsoft.AspNetCore.SignalR;
using Orleans;

namespace ConsoleGameServer.Management
{
    /// <summary>
    /// Singleton Orleans grain that manages and controls active game sessions.
    /// </summary>
    public class GameManagementGrain : Grain, IGameManagementGrain
    {
        private readonly ConcurrentDictionary<string, SessionMetadata> _sessionIndex = new();
        private readonly ConcurrentDictionary<string, string> _connectionToSession = new();
        private readonly ConcurrentDictionary<string, string> _worldRegistry = new(); // worldId -> worldId (for tracking)
        private readonly IHubContext<GameHub> _hubContext;
        private readonly GameSessionManager _sessionManager;
        private readonly IGrainFactory _grainFactory;

        private class SessionMetadata
        {
            public string SessionId { get; set; } = string.Empty;
            public string ConnectionId { get; set; } = string.Empty;
            public DateTime ConnectedAt { get; set; }
        }

        public GameManagementGrain(IHubContext<GameHub> hubContext, GameSessionManager sessionManager, IGrainFactory grainFactory)
        {
            _hubContext = hubContext ?? throw new ArgumentNullException(nameof(hubContext));
            _sessionManager = sessionManager ?? throw new ArgumentNullException(nameof(sessionManager));
            _grainFactory = grainFactory ?? throw new ArgumentNullException(nameof(grainFactory));
        }

        public override Task OnActivateAsync(CancellationToken cancellationToken)
        {
            var grainKey = this.GetPrimaryKeyString();
            Console.WriteLine($"[GameManagementGrain] Activated: {grainKey}");
            return base.OnActivateAsync(cancellationToken);
        }

        // Lifecycle Methods
        public Task RegisterSessionAsync(string sessionId, string connectionId)
        {
            if (string.IsNullOrEmpty(sessionId))
                throw new ArgumentNullException(nameof(sessionId));
            if (string.IsNullOrEmpty(connectionId))
                throw new ArgumentNullException(nameof(connectionId));

            var metadata = new SessionMetadata
            {
                SessionId = sessionId,
                ConnectionId = connectionId,
                ConnectedAt = DateTime.UtcNow
            };

            _sessionIndex[sessionId] = metadata;
            _connectionToSession[connectionId] = sessionId;

            Console.WriteLine($"[GameManagementGrain] Registered session {sessionId} for connection {connectionId}");
            return Task.CompletedTask;
        }

        public Task UnregisterSessionAsync(string sessionId)
        {
            if (string.IsNullOrEmpty(sessionId))
                throw new ArgumentNullException(nameof(sessionId));

            if (_sessionIndex.TryRemove(sessionId, out var metadata))
            {
                _connectionToSession.TryRemove(metadata.ConnectionId, out _);
                Console.WriteLine($"[GameManagementGrain] Unregistered session {sessionId}");
            }

            return Task.CompletedTask;
        }

        // Query Methods
        public Task<List<SessionInfo>> ListSessionsAsync()
        {
            var sessions = _sessionIndex.Values
                .Select(meta => BuildSessionInfo(meta.SessionId))
                .Where(info => info != null)
                .Cast<SessionInfo>()
                .ToList();

            return Task.FromResult(sessions);
        }

        public Task<SessionInfo?> GetSessionInfoAsync(string sessionId)
        {
            return Task.FromResult(BuildSessionInfo(sessionId));
        }

        public Task<SessionInfo?> GetSessionByConnectionIdAsync(string connectionId)
        {
            if (_connectionToSession.TryGetValue(connectionId, out var sessionId))
            {
                return Task.FromResult(BuildSessionInfo(sessionId));
            }
            return Task.FromResult<SessionInfo?>(null);
        }

        public Task<int> GetSessionCountAsync()
        {
            return Task.FromResult(_sessionIndex.Count);
        }

        // Vision Control Methods
        public async Task<OperationResult> SetDirectionalVisionAsync(string sessionId, bool enabled)
        {
            try
            {
                if (string.IsNullOrEmpty(sessionId))
                    return OperationResult.Error("Session ID cannot be null");

                if (!_sessionIndex.TryGetValue(sessionId, out var metadata))
                    return OperationResult.Error("Session not found");

                var session = _sessionManager.GetSession(metadata.ConnectionId);
                if (session == null)
                    return OperationResult.Error("Session not found in manager");

                // Set the vision mode
                session.DirectionalVisionMode = enabled;

                // Send updated perception to client
                var perception = session.GetPerception();
                await _hubContext.Clients.Client(metadata.ConnectionId)
                    .SendAsync("ReceivePerceptionUpdate", perception);

                Console.WriteLine($"[GameManagementGrain] Set directional vision to {enabled} for session {sessionId}");
                return OperationResult.Ok();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[GameManagementGrain] Error setting directional vision: {ex.Message}");
                return OperationResult.Error($"Failed to set directional vision: {ex.Message}");
            }
        }

        public async Task<OperationResult> SetFieldOfViewAsync(string sessionId, int degrees)
        {
            try
            {
                if (string.IsNullOrEmpty(sessionId))
                    return OperationResult.Error("Session ID cannot be null");

                if (degrees < 1 || degrees > 360)
                    return OperationResult.Error("FOV must be between 1 and 360 degrees");

                if (!_sessionIndex.TryGetValue(sessionId, out var metadata))
                    return OperationResult.Error("Session not found");

                var session = _sessionManager.GetSession(metadata.ConnectionId);
                if (session?.Player == null)
                    return OperationResult.Error("Session or player not found");

                // Set FOV on player entity
                var hasHeading = session.Player.Get<HasHeading>();
                if (hasHeading != null)
                {
                    hasHeading.FieldOfViewDegrees = degrees;

                    // Send updated perception
                    var perception = session.GetPerception();
                    await _hubContext.Clients.Client(metadata.ConnectionId)
                        .SendAsync("ReceivePerceptionUpdate", perception);

                    Console.WriteLine($"[GameManagementGrain] Set FOV to {degrees}° for session {sessionId}");
                    return OperationResult.Ok();
                }

                return OperationResult.Error("Player does not have heading component");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[GameManagementGrain] Error setting FOV: {ex.Message}");
                return OperationResult.Error($"Failed to set FOV: {ex.Message}");
            }
        }

        public Task<VisionStatus?> GetVisionStatusAsync(string sessionId)
        {
            try
            {
                if (string.IsNullOrEmpty(sessionId))
                    return Task.FromResult<VisionStatus?>(null);

                if (!_sessionIndex.TryGetValue(sessionId, out var metadata))
                    return Task.FromResult<VisionStatus?>(null);

                var session = _sessionManager.GetSession(metadata.ConnectionId);
                if (session == null)
                    return Task.FromResult<VisionStatus?>(null);

                var fov = 120; // Default
                if (session.Player != null)
                {
                    var hasHeading = session.Player.Get<HasHeading>();
                    if (hasHeading != null)
                        fov = hasHeading.FieldOfViewDegrees;
                }

                var status = new VisionStatus
                {
                    DirectionalVisionMode = session.DirectionalVisionMode,
                    HeadingDegrees = session.HeadingDegrees,
                    FieldOfViewDegrees = fov,
                    LightingMode = session.CurrentLightingMode,
                    VisionMode = session.CurrentVisionMode
                };

                return Task.FromResult<VisionStatus?>(status);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[GameManagementGrain] Error getting vision status: {ex.Message}");
                return Task.FromResult<VisionStatus?>(null);
            }
        }

        // Session Control Methods
        public async Task<OperationResult> SetLightingModeAsync(string sessionId, LightingMode mode)
        {
            try
            {
                if (string.IsNullOrEmpty(sessionId))
                    return OperationResult.Error("Session ID cannot be null");

                if (!Enum.IsDefined(typeof(LightingMode), mode))
                    return OperationResult.Error("Invalid lighting mode");

                if (!_sessionIndex.TryGetValue(sessionId, out var metadata))
                    return OperationResult.Error("Session not found");

                var session = _sessionManager.GetSession(metadata.ConnectionId);
                if (session == null)
                    return OperationResult.Error("Session not found in manager");

                session.CurrentLightingMode = mode;

                // Send updated perception
                var perception = session.GetPerception();
                await _hubContext.Clients.Client(metadata.ConnectionId)
                    .SendAsync("ReceivePerceptionUpdate", perception);

                Console.WriteLine($"[GameManagementGrain] Set lighting mode to {mode} for session {sessionId}");
                return OperationResult.Ok();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[GameManagementGrain] Error setting lighting mode: {ex.Message}");
                return OperationResult.Error($"Failed to set lighting mode: {ex.Message}");
            }
        }

        public async Task<OperationResult> SetVisionModeAsync(string sessionId, VisionMode mode)
        {
            try
            {
                if (string.IsNullOrEmpty(sessionId))
                    return OperationResult.Error("Session ID cannot be null");

                if (!Enum.IsDefined(typeof(VisionMode), mode))
                    return OperationResult.Error("Invalid vision mode");

                if (!_sessionIndex.TryGetValue(sessionId, out var metadata))
                    return OperationResult.Error("Session not found");

                var session = _sessionManager.GetSession(metadata.ConnectionId);
                if (session == null)
                    return OperationResult.Error("Session not found in manager");

                session.CurrentVisionMode = mode;

                // Send updated perception
                var perception = session.GetPerception();
                await _hubContext.Clients.Client(metadata.ConnectionId)
                    .SendAsync("ReceivePerceptionUpdate", perception);

                Console.WriteLine($"[GameManagementGrain] Set vision mode to {mode} for session {sessionId}");
                return OperationResult.Ok();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[GameManagementGrain] Error setting vision mode: {ex.Message}");
                return OperationResult.Error($"Failed to set vision mode: {ex.Message}");
            }
        }

        public Task<OperationResult> TerminateSessionAsync(string sessionId)
        {
            try
            {
                if (string.IsNullOrEmpty(sessionId))
                    return Task.FromResult(OperationResult.Error("Session ID cannot be null"));

                if (!_sessionIndex.TryGetValue(sessionId, out var metadata))
                    return Task.FromResult(OperationResult.Error("Session not found"));

                // Note: Disconnecting the client will trigger GameHub.OnDisconnectedAsync
                // which will call UnregisterSessionAsync
                // For now, we'll just remove from SessionManager
                _sessionManager.RemoveSession(metadata.ConnectionId);
                
                // Also remove from our index
                _sessionIndex.TryRemove(sessionId, out _);
                _connectionToSession.TryRemove(metadata.ConnectionId, out _);

                Console.WriteLine($"[GameManagementGrain] Terminated session {sessionId}");
                return Task.FromResult(OperationResult.Ok());
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[GameManagementGrain] Error terminating session: {ex.Message}");
                return Task.FromResult(OperationResult.Error($"Failed to terminate session: {ex.Message}"));
            }
        }

        public Task<OperationResult> SetTimeScaleAsync(string sessionId, double scale)
        {
            try
            {
                if (string.IsNullOrEmpty(sessionId))
                    return Task.FromResult(OperationResult.Error("Session ID cannot be null"));

                if (scale <= 0)
                    return Task.FromResult(OperationResult.Error("Time scale must be positive"));

                if (!_sessionIndex.TryGetValue(sessionId, out var metadata))
                    return Task.FromResult(OperationResult.Error("Session not found"));

                var session = _sessionManager.GetSession(metadata.ConnectionId);
                if (session == null)
                    return Task.FromResult(OperationResult.Error("Session not found in manager"));

                session.TimeScale = scale;

                Console.WriteLine($"[GameManagementGrain] Set time scale to {scale}x for session {sessionId}");
                return Task.FromResult(OperationResult.Ok());
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[GameManagementGrain] Error setting time scale: {ex.Message}");
                return Task.FromResult(OperationResult.Error($"Failed to set time scale: {ex.Message}"));
            }
        }

        // Helper Methods
        private SessionInfo? BuildSessionInfo(string sessionId)
        {
            if (!_sessionIndex.TryGetValue(sessionId, out var metadata))
                return null;

            var session = _sessionManager.GetSession(metadata.ConnectionId);
            if (session == null)
                return null;

            var fov = 120; // Default
            if (session.Player != null)
            {
                var hasHeading = session.Player.Get<HasHeading>();
                if (hasHeading != null)
                    fov = hasHeading.FieldOfViewDegrees;
            }

            return new SessionInfo
            {
                SessionId = metadata.SessionId,
                ConnectionId = metadata.ConnectionId,
                DirectionalVisionMode = session.DirectionalVisionMode,
                HeadingDegrees = session.HeadingDegrees,
                FieldOfViewDegrees = fov,
                LightingMode = session.CurrentLightingMode,
                VisionMode = session.CurrentVisionMode,
                TimeScale = session.TimeScale,
                ConnectedAt = metadata.ConnectedAt
            };
        }

        // World Management Methods
        public Task<List<WorldInfo>> ListWorldsAsync()
        {
            var worldInfos = new List<WorldInfo>();
            
            // Get info from all registered worlds
            var tasks = _worldRegistry.Keys
                .Select(async worldId =>
                {
                    var worldGrain = _grainFactory.GetGrain<IWorldGrain>(worldId);
                    var info = await worldGrain.GetInfoAsync();
                    return info;
                })
                .ToList();

            // Wait for all tasks and collect results
            var results = Task.WhenAll(tasks).GetAwaiter().GetResult();
            worldInfos.AddRange(results.Where(info => info != null)!);

            return Task.FromResult(worldInfos);
        }

        public async Task<WorldInfo?> GetWorldInfoAsync(string worldId)
        {
            if (!_worldRegistry.ContainsKey(worldId))
                return null;

            var worldGrain = _grainFactory.GetGrain<IWorldGrain>(worldId);
            return await worldGrain.GetInfoAsync();
        }

        public async Task<string> CreateWorldAsync(CreateWorldRequest request)
        {
            var worldId = $"world:{Guid.NewGuid()}";
            var worldGrain = _grainFactory.GetGrain<IWorldGrain>(worldId);

            var config = new WorldConfig
            {
                WorldId = worldId,
                Name = request.Name,
                Description = request.Description,
                GeneratorType = request.GeneratorType,
                GeneratorParameters = request.GeneratorParameters,
                NarrativeId = request.NarrativeId,
                MaxPlayers = request.MaxPlayers,
                Size = request.Size ?? new WorldSize { Width = 100, Height = 100, Depth = 1 },
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "system"
            };

            await worldGrain.InitializeAsync(config);
            _worldRegistry[worldId] = worldId;

            Console.WriteLine($"[GameManagementGrain] Created world {worldId} ({request.Name})");
            return worldId;
        }

        public async Task<OperationResult> PauseWorldAsync(string worldId)
        {
            try
            {
                if (!_worldRegistry.ContainsKey(worldId))
                    return OperationResult.Error("World not found");

                var worldGrain = _grainFactory.GetGrain<IWorldGrain>(worldId);
                await worldGrain.PauseAsync();

                Console.WriteLine($"[GameManagementGrain] Paused world {worldId}");
                return OperationResult.Ok();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[GameManagementGrain] Error pausing world: {ex.Message}");
                return OperationResult.Error($"Failed to pause world: {ex.Message}");
            }
        }

        public async Task<OperationResult> ResumeWorldAsync(string worldId)
        {
            try
            {
                if (!_worldRegistry.ContainsKey(worldId))
                    return OperationResult.Error("World not found");

                var worldGrain = _grainFactory.GetGrain<IWorldGrain>(worldId);
                await worldGrain.ResumeAsync();

                Console.WriteLine($"[GameManagementGrain] Resumed world {worldId}");
                return OperationResult.Ok();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[GameManagementGrain] Error resuming world: {ex.Message}");
                return OperationResult.Error($"Failed to resume world: {ex.Message}");
            }
        }

        public async Task<OperationResult> ShutdownWorldAsync(string worldId)
        {
            try
            {
                if (!_worldRegistry.ContainsKey(worldId))
                    return OperationResult.Error("World not found");

                var worldGrain = _grainFactory.GetGrain<IWorldGrain>(worldId);
                await worldGrain.ShutdownAsync();

                _worldRegistry.TryRemove(worldId, out _);

                Console.WriteLine($"[GameManagementGrain] Shut down world {worldId}");
                return OperationResult.Ok();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[GameManagementGrain] Error shutting down world: {ex.Message}");
                return OperationResult.Error($"Failed to shut down world: {ex.Message}");
            }
        }
    }
}

