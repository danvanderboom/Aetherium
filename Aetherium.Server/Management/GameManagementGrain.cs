using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Aetherium.Components;
using Aetherium.Model;
using Aetherium.Server.MultiWorld;
using Aetherium.Server.Agents.Tools;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Orleans;
using Aetherium.Server;
using ModelRelativeDirection = Aetherium.Model.RelativeDirection;

namespace Aetherium.Server.Management
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
        private readonly InteractionSystem _interactionSystem = new InteractionSystem();
        private readonly Aetherium.Server.Services.IWorldHost? _worldHost;
        private readonly Aetherium.Server.Services.WorldRegistry? _liveWorlds;
        private readonly IServiceProvider _serviceProvider;

        private class SessionMetadata
        {
            public string SessionId { get; set; } = string.Empty;
            public string ConnectionId { get; set; } = string.Empty;
            public DateTime ConnectedAt { get; set; }
            public DateTime LastActivityAt { get; set; }
        }

        // Default idle timeout for reaping abandoned headless sessions (env-overridable).
        private const int DefaultHeadlessIdleSeconds = 1800;
        private static int HeadlessIdleSeconds()
        {
            var raw = Environment.GetEnvironmentVariable("AETHERIUM_HEADLESS_IDLE_SECONDS");
            return int.TryParse(raw, out var v) && v > 0 ? v : DefaultHeadlessIdleSeconds;
        }

        private void TouchSession(string sessionId)
        {
            if (_sessionIndex.TryGetValue(sessionId, out var meta))
                meta.LastActivityAt = DateTime.UtcNow;
        }

        public GameManagementGrain(
            IHubContext<GameHub> hubContext, 
            GameSessionManager sessionManager, 
            IGrainFactory grainFactory,
            IServiceProvider serviceProvider)
        {
            _hubContext = hubContext ?? throw new ArgumentNullException(nameof(hubContext));
            _sessionManager = sessionManager ?? throw new ArgumentNullException(nameof(sessionManager));
            _grainFactory = grainFactory ?? throw new ArgumentNullException(nameof(grainFactory));
            _worldHost = serviceProvider.GetService<Aetherium.Server.Services.IWorldHost>();
            _liveWorlds = serviceProvider.GetService<Aetherium.Server.Services.WorldRegistry>();
            _serviceProvider = serviceProvider;
        }

        public override Task OnActivateAsync(CancellationToken cancellationToken)
        {
            var grainKey = this.GetPrimaryKeyString();
            Console.WriteLine($"[GameManagementGrain] Activated: {grainKey}");

            // Best-effort background reaping of abandoned headless sessions.
            RegisterTimer(
                async _ =>
                {
                    try { await ReapIdleHeadlessSessionsAsync(HeadlessIdleSeconds()); }
                    catch (Exception ex) { Console.WriteLine($"[GameManagementGrain] Reaper error: {ex.Message}"); }
                },
                null,
                TimeSpan.FromMinutes(5),
                TimeSpan.FromMinutes(5));

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
                ConnectedAt = DateTime.UtcNow,
                LastActivityAt = DateTime.UtcNow
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

        // ============================================================
        // Gameplay control + perception (for agents)
        // ============================================================

        // ---- Operator / headless driving ------------------------------------------------

        public Task<HeadlessSessionResult> CreateHeadlessSessionAsync(string worldId, int? startX, int? startY, int? startZ, string? profile)
        {
            if (!OperatorAccess.IsEnabled())
                return Task.FromResult(HeadlessSessionResult.Error("Operator access is disabled"));

            if (string.IsNullOrEmpty(worldId))
                return Task.FromResult(HeadlessSessionResult.Error("World ID cannot be null or empty"));

            var world = _liveWorlds?.Resolve(worldId);
            if (world == null)
                return Task.FromResult(HeadlessSessionResult.Error($"World not found or not initialized in this process: {worldId}"));

            WorldLocation? start = (startX.HasValue && startY.HasValue)
                ? new WorldLocation(startX.Value, startY.Value, startZ ?? 0)
                : null;

            try
            {
                var connectionId = $"headless:{Guid.NewGuid()}";
                var session = _sessionManager.CreateHeadlessSession(connectionId, worldId, world, start);

                // Register in the session index (mirrors RegisterSessionAsync) so all existing
                // verbs (perception, move, ExecuteTool, agent attach) resolve this session.
                _sessionIndex[session.SessionId] = new SessionMetadata
                {
                    SessionId = session.SessionId,
                    ConnectionId = connectionId,
                    ConnectedAt = DateTime.UtcNow,
                    LastActivityAt = DateTime.UtcNow
                };
                _connectionToSession[connectionId] = session.SessionId;

                // profile is reserved for future tool-profile wiring; ExecuteToolAsync currently
                // applies the Player profile to all sessions.
                Console.WriteLine($"[GameManagementGrain] Created headless session {session.SessionId} in world {worldId} (profile={profile ?? "player"})");
                return Task.FromResult(HeadlessSessionResult.Ok(session.SessionId, worldId, connectionId));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[GameManagementGrain] Error creating headless session: {ex.Message}");
                return Task.FromResult(HeadlessSessionResult.Error($"Failed to create headless session: {ex.Message}"));
            }
        }

        public Task<int> ReapIdleHeadlessSessionsAsync(int maxIdleSeconds)
        {
            var cutoff = DateTime.UtcNow.AddSeconds(-maxIdleSeconds);
            int reaped = 0;

            foreach (var meta in _sessionIndex.Values.ToList())
            {
                // Only reap headless sessions; client-backed sessions are removed on disconnect.
                var session = _sessionManager.GetSession(meta.ConnectionId);
                if (session == null || !session.IsHeadless)
                    continue;

                if (meta.LastActivityAt <= cutoff)
                {
                    _sessionIndex.TryRemove(meta.SessionId, out _);
                    _connectionToSession.TryRemove(meta.ConnectionId, out _);
                    _sessionManager.RemoveSession(meta.ConnectionId);
                    reaped++;
                    Console.WriteLine($"[GameManagementGrain] Reaped idle headless session {meta.SessionId} (idle since {meta.LastActivityAt:o})");
                }
            }

            return Task.FromResult(reaped);
        }

        public Task<string?> GetPerceptionAsync(string sessionId, bool absoluteCoordinates)
        {
            // Absolute coordinates are a god-view read; gate them behind operator access.
            if (absoluteCoordinates && !OperatorAccess.IsEnabled())
                return Task.FromResult<string?>(null);

            TouchSession(sessionId);

            if (string.IsNullOrEmpty(sessionId))
                return Task.FromResult<string?>(null);

            if (!_sessionIndex.TryGetValue(sessionId, out var metadata))
                return Task.FromResult<string?>(null);

            var session = _sessionManager.GetSession(metadata.ConnectionId);
            if (session == null)
                return Task.FromResult<string?>(null);

            try
            {
                var perception = session.GetPerception(absoluteCoordinates);
                return Task.FromResult<string?>(System.Text.Json.JsonSerializer.Serialize(perception));
            }
            catch
            {
                return Task.FromResult<string?>(null);
            }
        }

        public Task<string?> GetWorldSnapshotAsync(string worldId)
        {
            if (!OperatorAccess.IsEnabled())
                return Task.FromResult<string?>(null);

            if (string.IsNullOrEmpty(worldId))
                return Task.FromResult<string?>(null);

            var world = _liveWorlds?.Resolve(worldId);
            if (world == null)
                return Task.FromResult<string?>(null);

            try
            {
                var mapId = _liveWorlds?.GetPrimaryMapId(worldId) ?? worldId;
                var snapshot = WorldSnapshotBuilder.Build(world, worldId, mapId);
                if (snapshot.Truncated)
                    Console.WriteLine($"[GameManagementGrain] World snapshot for {worldId} truncated (emitted {snapshot.Entities.Count}/{snapshot.EntityCount} entities, {snapshot.Tiles.Count}/{snapshot.TileCount} tiles)");
                return Task.FromResult<string?>(System.Text.Json.JsonSerializer.Serialize(snapshot));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[GameManagementGrain] Error building world snapshot for {worldId}: {ex.Message}");
                return Task.FromResult<string?>(null);
            }
        }

        public Task<string?> GetMemoryAsync(string sessionId)
        {
            // Memories carry absolute world coordinates — a god-view read, gated like
            // absolute perception and world snapshots.
            if (!OperatorAccess.IsEnabled())
                return Task.FromResult<string?>(null);

            if (string.IsNullOrEmpty(sessionId) || !_sessionIndex.TryGetValue(sessionId, out var metadata))
                return Task.FromResult<string?>(null);

            var session = _sessionManager.GetSession(metadata.ConnectionId);
            var player = session?.Player;
            if (session == null || player == null || !player.Has<Aetherium.Components.Memory>())
                return Task.FromResult<string?>(null);

            TouchSession(sessionId);
            try
            {
                var memory = player.Get<Aetherium.Components.Memory>();
                var halfLife = session.World.MemoryPolicy.DecayHalfLifeSeconds;

                var dto = new CharacterMemoryDto
                {
                    SessionId = sessionId,
                    LocationsTracked = memory.LocationsTracked,
                    TotalMemories = memory.SpaceTimeMemoriesTracked,
                    TotalImpressions = memory.SpaceTimeMemoryImpressions
                };

                foreach (var m in memory.AllSpaceTimeMemories)
                {
                    dto.Memories.Add(new MemoryEntryDto
                    {
                        Location = new WorldLocationDto(m.Location.X, m.Location.Y, m.Location.Z),
                        ContentType = m.ContentType,
                        Content = m.Content,
                        Strength = m.Strength,
                        EffectiveStrength = Aetherium.Core.MemoryPolicy.EffectiveStrength(m.Strength, m.TimeSinceLastSeen, halfLife),
                        Impressions = m.Impressions,
                        LastEventTime = m.LastEventTime
                    });
                }

                return Task.FromResult<string?>(System.Text.Json.JsonSerializer.Serialize(dto));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[GameManagementGrain] Error reading memory for session {sessionId}: {ex.Message}");
                return Task.FromResult<string?>(null);
            }
        }

        public async Task<ToolExecutionResultDto> ExecuteWorldToolAsync(string worldId, string toolId, Dictionary<string, object> args)
        {
            if (!OperatorAccess.IsEnabled())
                return new ToolExecutionResultDto { Success = false, Message = "Operator access is disabled" };

            if (string.IsNullOrEmpty(worldId))
                return new ToolExecutionResultDto { Success = false, Message = "World ID cannot be null or empty" };

            var world = _liveWorlds?.Resolve(worldId);
            if (world == null)
                return new ToolExecutionResultDto { Success = false, Message = $"World not found or not initialized in this process: {worldId}" };

            var toolRegistry = ServiceProvider.GetService(typeof(Aetherium.Server.Agents.Tools.AgentToolRegistry))
                as Aetherium.Server.Agents.Tools.AgentToolRegistry;
            if (toolRegistry == null)
                return new ToolExecutionResultDto { Success = false, Message = "Tool registry not available" };

            var tool = toolRegistry.GetTool(toolId);
            if (tool == null)
                return new ToolExecutionResultDto { Success = false, Message = $"Tool not found: {toolId}" };

            // Only world-building tools may run here: character tools (move, pickup, ...) need a
            // session context and must not execute through the god-view path.
            if (!tool.RequiredCapabilities.Contains("world_edit"))
                return new ToolExecutionResultDto { Success = false, Message = $"Tool '{toolId}' is not a world-building tool (requires world_edit)" };

            try
            {
                var context = new Aetherium.Server.Agents.Tools.WorldBuildingToolContext(world, ServiceProvider);
                var result = await tool.ExecuteAsync(context, args ?? new Dictionary<string, object>());
                Console.WriteLine($"[GameManagementGrain] Executed world tool '{toolId}' on {worldId}: {(result.Success ? "Success" : "Failed")} - {result.Message}");
                return result.ToDto();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[GameManagementGrain] Error executing world tool {toolId} on {worldId}: {ex.Message}");
                return new ToolExecutionResultDto { Success = false, Message = $"Error executing world tool: {ex.Message}" };
            }
        }

        public Task<string?> GetPerceptionAsync(string sessionId)
        {
            if (string.IsNullOrEmpty(sessionId))
                return Task.FromResult<string?>(null);

            if (!_sessionIndex.TryGetValue(sessionId, out var metadata))
                return Task.FromResult<string?>(null);

            var session = _sessionManager.GetSession(metadata.ConnectionId);
            if (session == null)
                return Task.FromResult<string?>(null);

            TouchSession(sessionId);
            try
            {
                var perception = session.GetPerception();
                var json = System.Text.Json.JsonSerializer.Serialize(perception);
                return Task.FromResult<string?>(json);
            }
            catch
            {
                return Task.FromResult<string?>(null);
            }
        }

        public async Task<OperationResult> MoveAsync(string sessionId, string direction)
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

                // Normalize direction
                var d = (direction ?? string.Empty).Trim().ToUpperInvariant();

                // Support relative directions
                if (d is "F" or "FORWARD" or "B" or "BACKWARD" or "L" or "LEFT" or "R" or "RIGHT")
                {
                    var rel = d switch
                    {
                        "F" or "FORWARD" => ModelRelativeDirection.Forward,
                        "B" or "BACKWARD" => ModelRelativeDirection.Backward,
                        "L" or "LEFT" => ModelRelativeDirection.Left,
                        "R" or "RIGHT" => ModelRelativeDirection.Right,
                        _ => ModelRelativeDirection.Forward
                    };

                    var outcome = session.MoveView(rel, 1);
                    if (!outcome.Success)
                        return OperationResult.Error(outcome.BlockedReason ?? "Blocked");
                }
                else if (d is "N" or "NORTH" or "E" or "EAST" or "S" or "SOUTH" or "W" or "WEST")
                {
                    // Absolute move without changing long-term heading: rotate temporarily, move forward, rotate back
                    int targetDegrees = d switch
                    {
                        "E" or "EAST" => 90,
                        "S" or "SOUTH" => 180,
                        "W" or "WEST" => 270,
                        _ => 0
                    };

                    var original = session.HeadingDegrees;
                    int delta = targetDegrees - original;
                    session.RotateView(delta);
                    var outcome = session.MoveView(ModelRelativeDirection.Forward, 1);
                    session.RotateView(-delta);
                    if (!outcome.Success)
                        return OperationResult.Error(outcome.BlockedReason ?? "Blocked");
                }
                else
                {
                    return OperationResult.Error("Invalid direction");
                }

                // Send updated perception to client for visibility
                var perception = session.GetPerception();
                await _hubContext.Clients.Client(metadata.ConnectionId).SendAsync("ReceivePerceptionUpdate", perception);

                return OperationResult.Ok();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[GameManagementGrain] Error moving: {ex.Message}");
                return OperationResult.Error($"Failed to move: {ex.Message}");
            }
        }

        public async Task<OperationResult> PickupAsync(string sessionId, string targetEntityId)
        {
            try
            {
                if (!_sessionIndex.TryGetValue(sessionId, out var metadata))
                    return OperationResult.Error("Session not found");

                var session = _sessionManager.GetSession(metadata.ConnectionId);
                if (session == null)
                    return OperationResult.Error("Session not found in manager");

                // Serialize against hub-thread mutations on the same legacy session (P0-12).
                var result = session.WithStateLock(() => _interactionSystem.TryPickup(session, targetEntityId));

                var perception = session.GetPerception();
                await _hubContext.Clients.Client(metadata.ConnectionId).SendAsync("ReceivePerceptionUpdate", perception);

                return result.Success ? OperationResult.Ok() : OperationResult.Error(result.Reason);
            }
            catch (Exception ex)
            {
                return OperationResult.Error($"Pickup failed: {ex.Message}");
            }
        }

        public async Task<OperationResult> DropAsync(string sessionId, string itemEntityId)
        {
            try
            {
                if (!_sessionIndex.TryGetValue(sessionId, out var metadata))
                    return OperationResult.Error("Session not found");

                var session = _sessionManager.GetSession(metadata.ConnectionId);
                if (session == null)
                    return OperationResult.Error("Session not found in manager");

                var result = session.WithStateLock(() => _interactionSystem.TryDrop(session, itemEntityId));
                var perception = session.GetPerception();
                await _hubContext.Clients.Client(metadata.ConnectionId).SendAsync("ReceivePerceptionUpdate", perception);
                return result.Success ? OperationResult.Ok() : OperationResult.Error(result.Reason);
            }
            catch (Exception ex)
            {
                return OperationResult.Error($"Drop failed: {ex.Message}");
            }
        }

        public async Task<OperationResult> UseAsync(string sessionId, string itemEntityId, string onEntityId)
        {
            try
            {
                if (!_sessionIndex.TryGetValue(sessionId, out var metadata))
                    return OperationResult.Error("Session not found");

                var session = _sessionManager.GetSession(metadata.ConnectionId);
                if (session == null)
                    return OperationResult.Error("Session not found in manager");

                var result = session.WithStateLock(() => _interactionSystem.TryUse(session, itemEntityId, onEntityId));
                var perception = session.GetPerception();
                await _hubContext.Clients.Client(metadata.ConnectionId).SendAsync("ReceivePerceptionUpdate", perception);
                return result.Success ? OperationResult.Ok() : OperationResult.Error(result.Reason);
            }
            catch (Exception ex)
            {
                return OperationResult.Error($"Use failed: {ex.Message}");
            }
        }

        public async Task<OperationResult> OpenAsync(string sessionId, string targetEntityId)
        {
            try
            {
                if (!_sessionIndex.TryGetValue(sessionId, out var metadata))
                    return OperationResult.Error("Session not found");

                var session = _sessionManager.GetSession(metadata.ConnectionId);
                if (session == null)
                    return OperationResult.Error("Session not found in manager");

                var result = session.WithStateLock(() => _interactionSystem.TryOpen(session, targetEntityId));
                var perception = session.GetPerception();
                await _hubContext.Clients.Client(metadata.ConnectionId).SendAsync("ReceivePerceptionUpdate", perception);
                return result.Success ? OperationResult.Ok() : OperationResult.Error(result.Reason);
            }
            catch (Exception ex)
            {
                return OperationResult.Error($"Open failed: {ex.Message}");
            }
        }

        public async Task<OperationResult> CloseAsync(string sessionId, string targetEntityId)
        {
            try
            {
                if (!_sessionIndex.TryGetValue(sessionId, out var metadata))
                    return OperationResult.Error("Session not found");

                var session = _sessionManager.GetSession(metadata.ConnectionId);
                if (session == null)
                    return OperationResult.Error("Session not found in manager");

                var result = session.WithStateLock(() => _interactionSystem.TryClose(session, targetEntityId));
                var perception = session.GetPerception();
                await _hubContext.Clients.Client(metadata.ConnectionId).SendAsync("ReceivePerceptionUpdate", perception);
                return result.Success ? OperationResult.Ok() : OperationResult.Error(result.Reason);
            }
            catch (Exception ex)
            {
                return OperationResult.Error($"Close failed: {ex.Message}");
            }
        }

        // Helper Methods
        private SessionInfo? BuildSessionInfo(string sessionId)
        {
            if (!_sessionIndex.TryGetValue(sessionId, out var metadata))
                return null;

            var session = _sessionManager.GetSession(metadata.ConnectionId);
            if (session == null)
            {
                return new SessionInfo
                {
                    SessionId = metadata.SessionId,
                    ConnectionId = metadata.ConnectionId,
                    DirectionalVisionMode = false,
                    HeadingDegrees = 0,
                    FieldOfViewDegrees = 120,
                    LightingMode = LightingMode.Torch,
                    VisionMode = VisionMode.Normal,
                    TimeScale = 1.0,
                    ConnectedAt = metadata.ConnectedAt
                };
            }

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
        public async Task<List<WorldInfo>> ListWorldsAsync()
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
            var results = await Task.WhenAll(tasks);
            worldInfos.AddRange(results.Where(info => info != null)!);

            return worldInfos;
        }

        public async Task<WorldInfo?> GetWorldInfoAsync(string worldId)
        {
            // Try to fetch info from the grain regardless of registry membership
            var worldGrain = _grainFactory.GetGrain<IWorldGrain>(worldId);
            var info = await worldGrain.GetInfoAsync();

            // If the world is not in our registry, treat brand-new, uninitialized activations as non-existent
            if (!_worldRegistry.ContainsKey(worldId))
            {
                if (info == null)
                    return null;

                bool looksUninitialized = string.IsNullOrWhiteSpace(info.Name)
                                           && info.CreatedAt == default
                                           && (info.MapIds == null || info.MapIds.Count == 0)
                                           && info.PlayerCount == 0;
                if (looksUninitialized)
                    return null;
            }

            return info;
        }

        public async Task<string> CreateWorldAsync(CreateWorldRequest request)
        {
            // Check if generator type is a hub template and resolve it
            WorldConfig? hubConfig = null;
            var hubTemplateResolver = _serviceProvider.GetService<Aetherium.Server.HubWorld.HubTemplateResolver>();
            if (hubTemplateResolver != null && !string.IsNullOrEmpty(request.GeneratorType))
            {
                hubConfig = await hubTemplateResolver.TryResolveHubAsync(request.GeneratorType, request, request.ClusterId);
            }

            // Use IWorldHost if available (new system), otherwise fallback to direct grain creation
            if (_worldHost != null)
            {
                var template = new Aetherium.Model.Worlds.WorldTemplate
                {
                    Name = hubConfig?.Name ?? request.Name,
                    Description = hubConfig?.Description ?? request.Description,
                    GeneratorType = hubConfig?.GeneratorType ?? (string.IsNullOrWhiteSpace(request.GeneratorType) ? "rooms-and-corridors" : request.GeneratorType),
                    GeneratorParameters = hubConfig?.GeneratorParameters ?? (request.GeneratorParameters ?? new Dictionary<string, object>()),
                    MaxPlayers = hubConfig?.MaxPlayers ?? request.MaxPlayers,
                    NarrativeId = hubConfig?.NarrativeId ?? request.NarrativeId,
                    ClusterId = hubConfig?.ClusterId ?? request.ClusterId,
                    DeathPolicy = hubConfig?.DeathPolicy ?? request.DeathPolicy,
                    AbilityConfig = hubConfig?.AbilityConfig ?? request.AbilityConfig,
                    ProgressionConfig = hubConfig?.ProgressionConfig ?? request.ProgressionConfig,
                    FactionConfig = hubConfig?.FactionConfig ?? request.FactionConfig,
                    Size = (hubConfig?.Size ?? request.Size) is { } size
                        ? new Aetherium.Model.Worlds.WorldDimensions { Width = size.Width, Height = size.Height, Depth = size.Depth }
                        : null,
                    GameDefinitionId = hubConfig?.GameDefinitionId ?? request.GameDefinitionId,
                    GameDefinitionVersion = hubConfig?.GameDefinitionVersion ?? request.GameDefinitionVersion,
                    ContentConfig = hubConfig?.ContentConfig ?? request.ContentConfig,
                    EcaConfig = hubConfig?.EcaConfig ?? request.EcaConfig,
                    Topology = hubConfig?.Topology ?? request.Topology
                };

                // Default to public world
                var acl = new Aetherium.Model.Worlds.WorldAcl
                {
                    AccessLevel = Aetherium.Model.Worlds.WorldAccessLevel.Public
                };

                var worldId = await _worldHost.CreateWorldAsync(template, acl);
                _worldRegistry[worldId.Value] = worldId.Value;

                Console.WriteLine($"[GameManagementGrain] Created world {worldId.Value} ({template.Name}) via IWorldHost{(hubConfig != null ? " (hub)" : "")}");
                return worldId.Value;
            }
            else
            {
                // Fallback to direct grain creation (for backwards compatibility)
                var config = hubConfig ?? new WorldConfig
                {
                    WorldId = $"world:{Guid.NewGuid()}",
                    Name = request.Name,
                    Description = request.Description,
                    GeneratorType = string.IsNullOrWhiteSpace(request.GeneratorType) ? "rooms-and-corridors" : request.GeneratorType,
                    GeneratorParameters = request.GeneratorParameters ?? new Dictionary<string, object>(),
                    NarrativeId = request.NarrativeId,
                    MaxPlayers = request.MaxPlayers,
                    Size = request.Size ?? new WorldSize { Width = 100, Height = 100, Depth = 1 },
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = "system",
                    ClusterId = request.ClusterId,
                    DeathPolicy = request.DeathPolicy,
                    AbilityConfig = request.AbilityConfig,
                    ProgressionConfig = request.ProgressionConfig,
                    FactionConfig = request.FactionConfig,
                    GameDefinitionId = request.GameDefinitionId,
                    GameDefinitionVersion = request.GameDefinitionVersion,
                    ContentConfig = request.ContentConfig,
                    EcaConfig = request.EcaConfig,
                    Topology = request.Topology
                };

                // Ensure WorldId is set
                if (string.IsNullOrEmpty(config.WorldId))
                {
                    config.WorldId = $"world:{Guid.NewGuid()}";
                }

                var worldId = config.WorldId;
                var worldGrain = _grainFactory.GetGrain<IWorldGrain>(worldId);

                await worldGrain.InitializeAsync(config);
                _worldRegistry[worldId] = worldId;

                Console.WriteLine($"[GameManagementGrain] Created world {worldId} ({config.Name}) via direct grain{(hubConfig != null ? " (hub)" : "")}");
                return worldId;
            }
        }

        // --- Game definitions (add-game-definition-loader) ---

        public Task<List<Aetherium.Model.Games.GameDefinitionSummaryDto>> ListGameDefinitionsAsync()
        {
            var registry = _serviceProvider.GetService<Aetherium.Server.Games.GameDefinitionRegistry>();
            return Task.FromResult(registry?.ListSummaries() ?? new List<Aetherium.Model.Games.GameDefinitionSummaryDto>());
        }

        public async Task<Aetherium.Model.Games.GameInstanceResult> CreateGameInstanceAsync(string gameDefinitionId, string? instanceName = null)
        {
            var registry = _serviceProvider.GetService<Aetherium.Server.Games.GameDefinitionRegistry>();
            if (registry == null)
                return Aetherium.Model.Games.GameInstanceResult.Fail("No game definition registry is configured on this server.");

            if (!registry.TryGet(gameDefinitionId, out var definition) || definition == null)
                return Aetherium.Model.Games.GameInstanceResult.Fail($"Unknown game definition '{gameDefinitionId}'.");

            var request = Aetherium.Server.Games.GameDefinitionMapper.ToCreateWorldRequest(definition, instanceName);
            var worldId = await CreateWorldAsync(request);

            Console.WriteLine($"[GameManagementGrain] Created game instance {worldId} of {definition.Id} v{definition.Version}");
            return Aetherium.Model.Games.GameInstanceResult.Ok(worldId);
        }

        public async Task<List<WorldInfo>> ListGameInstancesAsync(string gameDefinitionId)
        {
            var worlds = await ListWorldsAsync();
            return worlds.Where(w => w.GameDefinitionId == gameDefinitionId).ToList();
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

        // World ACL and Invites
        public async Task<List<Aetherium.Model.Worlds.WorldSummary>> ListWorldsWithAclAsync(Aetherium.Model.Worlds.WorldQuery query)
        {
            if (_worldHost == null)
                return new List<Aetherium.Model.Worlds.WorldSummary>();

            try
            {
                var worlds = await _worldHost.ListWorldsAsync(query);
                return worlds.ToList();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[GameManagementGrain] Error listing worlds with ACL: {ex.Message}");
                return new List<Aetherium.Model.Worlds.WorldSummary>();
            }
        }

        public async Task<OperationResult> SetWorldAclAsync(string worldId, Aetherium.Model.Worlds.WorldAcl acl)
        {
            if (_worldHost == null)
                return OperationResult.Error("World hosting service not available");

            try
            {
                var worldIdValue = new Aetherium.Model.Worlds.WorldId(worldId);
                await _worldHost.SetWorldAclAsync(worldIdValue, acl);
                Console.WriteLine($"[GameManagementGrain] Set ACL for world {worldId}");
                return OperationResult.Ok();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[GameManagementGrain] Error setting world ACL: {ex.Message}");
                return OperationResult.Error($"Failed to set world ACL: {ex.Message}");
            }
        }

        public async Task<Aetherium.Model.Worlds.WorldAcl?> GetWorldAclAsync(string worldId)
        {
            if (_worldHost == null)
                return null;

            try
            {
                var aclGrain = _grainFactory.GetGrain<Aetherium.Server.MultiWorld.IWorldAclGrain>(worldId);
                return await aclGrain.GetAclAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[GameManagementGrain] Error getting world ACL: {ex.Message}");
                return null;
            }
        }

        public async Task<string> InvitePlayerAsync(string worldId, string playerId)
        {
            if (_worldHost == null)
                throw new InvalidOperationException("World hosting service not available");

            try
            {
                var worldIdValue = new Aetherium.Model.Worlds.WorldId(worldId);
                var playerIdValue = new Aetherium.Model.Worlds.PlayerId(playerId);
                var inviteId = await _worldHost.InviteAsync(worldIdValue, playerIdValue);
                Console.WriteLine($"[GameManagementGrain] Invited player {playerId} to world {worldId}");
                return inviteId.Value;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[GameManagementGrain] Error inviting player: {ex.Message}");
                throw;
            }
        }

        public async Task<OperationResult> AcceptInviteAsync(string inviteId)
        {
            if (_worldHost == null)
                return OperationResult.Error("World hosting service not available");

            try
            {
                var inviteIdValue = new Aetherium.Model.Worlds.InviteId(inviteId);
                var success = await _worldHost.AcceptInviteAsync(inviteIdValue);
                if (success)
                {
                    Console.WriteLine($"[GameManagementGrain] Accepted invite {inviteId}");
                    return OperationResult.Ok();
                }
                else
                {
                    return OperationResult.Error("Invite not found or already accepted");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[GameManagementGrain] Error accepting invite: {ex.Message}");
                return OperationResult.Error($"Failed to accept invite: {ex.Message}");
            }
        }

        public Task<List<ToolInfoDto>> ListAvailableToolsAsync(string? profileName = null)
        {
            var toolRegistry = ServiceProvider.GetService(typeof(Aetherium.Server.Agents.Tools.AgentToolRegistry)) 
                as Aetherium.Server.Agents.Tools.AgentToolRegistry;

            if (toolRegistry == null)
                return Task.FromResult(new List<ToolInfoDto>());

            var profile = string.IsNullOrEmpty(profileName)
                ? Aetherium.Server.Agents.Tools.AgentToolProfile.Player // Default to Player (for all game characters)
                : Aetherium.Server.Agents.Tools.AgentToolProfile.GetPredefinedProfile(profileName);

            var tools = toolRegistry.GetToolsForProfile(profile)
                .Select(t => t.ToDto())
                .ToList();

            return Task.FromResult(tools);
        }

        public async Task<ToolExecutionResultDto> ExecuteToolAsync(string toolId, string sessionId, Dictionary<string, object> args)
        {
            try
            {
                if (string.IsNullOrEmpty(toolId))
                    return new ToolExecutionResultDto { Success = false, Message = "Tool ID cannot be null or empty" };

                if (string.IsNullOrEmpty(sessionId))
                    return new ToolExecutionResultDto { Success = false, Message = "Session ID cannot be null or empty" };

                // Get tool registry
                var toolRegistry = ServiceProvider.GetService(typeof(Aetherium.Server.Agents.Tools.AgentToolRegistry)) 
                    as Aetherium.Server.Agents.Tools.AgentToolRegistry;

                if (toolRegistry == null)
                    return new ToolExecutionResultDto { Success = false, Message = "Tool registry not available" };

                // Get the tool
                var tool = toolRegistry.GetTool(toolId);
                if (tool == null)
                    return new ToolExecutionResultDto { Success = false, Message = $"Tool not found: {toolId}" };

                // Get session metadata
                if (!_sessionIndex.TryGetValue(sessionId, out var metadata))
                    return new ToolExecutionResultDto { Success = false, Message = $"Session not found: {sessionId}" };

                // Get the actual session
                var session = _sessionManager.GetSession(metadata.ConnectionId);
                if (session == null)
                    return new ToolExecutionResultDto { Success = false, Message = "Session not found in session manager" };

                // Use Player profile capabilities (default for game sessions)
                var profile = Aetherium.Server.Agents.Tools.AgentToolProfile.Player;

                // Check if tool is allowed for this profile
                if (!profile.IsToolAllowed(tool))
                    return new ToolExecutionResultDto { Success = false, Message = $"Tool '{toolId}' is not allowed for the Player profile" };

                // Create execution context. Phase 2d: the InteractionSystem field
                // is removed from ToolExecutionContext; the MutationGateway is the
                // single mutation entry point and auto-falls-back to LocalMutationGateway
                // when Session is set (which it is here for agent-driven sessions).
                var context = new Aetherium.Server.Agents.Tools.ToolExecutionContext
                {
                    SessionId = sessionId,
                    ConnectionId = metadata.ConnectionId,
                    Session = session,
                    ManagementGrain = this,
                    GrantedCapabilities = new HashSet<string>(profile.GrantedCapabilities),
                    ServiceProvider = ServiceProvider
                };

                // Execute the tool
                var result = await tool.ExecuteAsync(context, args);

                // Send updated perception to client if successful
                if (result.Success)
                {
                    var perception = session.GetPerception();
                    await _hubContext.Clients.Client(metadata.ConnectionId)
                        .SendAsync("ReceivePerceptionUpdate", perception);
                }

                Console.WriteLine($"[GameManagementGrain] Executed tool '{toolId}' for session {sessionId}: {(result.Success ? "Success" : "Failed")} - {result.Message}");

                return result.ToDto();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[GameManagementGrain] Error executing tool {toolId} for session {sessionId}: {ex.Message}");
                return new ToolExecutionResultDto
                {
                    Success = false,
                    Message = $"Error executing tool: {ex.Message}"
                };
            }
        }

        private const int MaxBatchActions = 1000;

        public async Task<List<BatchActionResultDto>> ExecuteToolBatchAsync(string sessionId, List<ScriptedActionDto> actions, bool stopOnError)
        {
            var results = new List<BatchActionResultDto>();

            if (actions == null || actions.Count == 0)
                return results;

            if (actions.Count > MaxBatchActions)
            {
                results.Add(new BatchActionResultDto
                {
                    Index = 0,
                    Tool = string.Empty,
                    Success = false,
                    Message = $"Batch too large: {actions.Count} actions (max {MaxBatchActions})"
                });
                return results;
            }

            // Fail fast on an unknown session with a single result rather than N identical failures.
            if (string.IsNullOrEmpty(sessionId) || !_sessionIndex.ContainsKey(sessionId))
            {
                results.Add(new BatchActionResultDto
                {
                    Index = 0,
                    Tool = string.Empty,
                    Success = false,
                    Message = $"Session not found: {sessionId}"
                });
                return results;
            }

            // Runs inside a single grain turn: no other management call interleaves against this
            // session mid-batch, so ordering is deterministic. Each step reuses ExecuteToolAsync.
            for (int i = 0; i < actions.Count; i++)
            {
                var action = actions[i] ?? new ScriptedActionDto();
                var args = action.Args ?? new Dictionary<string, object>();

                ToolExecutionResultDto stepResult;
                try
                {
                    stepResult = await ExecuteToolAsync(action.Tool, sessionId, args);
                }
                catch (Exception ex)
                {
                    stepResult = new ToolExecutionResultDto { Success = false, Message = $"Error executing tool: {ex.Message}" };
                }

                results.Add(new BatchActionResultDto
                {
                    Index = i,
                    Tool = action.Tool,
                    Success = stepResult.Success,
                    Message = stepResult.Message
                });

                if (stopOnError && !stepResult.Success)
                    break;
            }

            return results;
        }
    }
}


