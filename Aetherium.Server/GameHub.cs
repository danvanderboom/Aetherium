using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Aetherium.Components;
using Aetherium.Core;
using Aetherium.WorldBuilders;
using Aetherium.Model;
using Aetherium.Server.Agents.Tools;
using Aetherium.Server.Management;
using Aetherium.Server.MultiWorld;
using Aetherium.Server.Narrative.Consequence;
using Microsoft.AspNetCore.SignalR;
using Orleans;

namespace Aetherium.Server
{
    public class GameHub : Hub
    {
        private readonly GameSessionManager sessionManager;
        private readonly InteractionSystem interactionSystem = new InteractionSystem();
        private readonly IClusterClient? clusterClient;
        private readonly Aetherium.Server.Agents.Tools.AgentToolRegistry? toolRegistry;

        public GameHub(GameSessionManager sessionManager, IClusterClient? clusterClient = null, Aetherium.Server.Agents.Tools.AgentToolRegistry? toolRegistry = null)
        {
            this.sessionManager = sessionManager;
            this.clusterClient = clusterClient;
            this.toolRegistry = toolRegistry;
        }

        private IGameManagementGrain? GetManagementGrain()
        {
            if (clusterClient == null) return null; // Orleans disabled
            return clusterClient.GetGrain<IGameManagementGrain>("GLOBAL");
        }

        /// <summary>
        /// Processes a narrative event through the consequence engine if narrative is enabled.
        /// Non-breaking: only processes if cluster client and world/narrative context are available.
        /// </summary>
        private async Task ProcessNarrativeEventAsync(GameSession session, string eventType, Dictionary<string, object> eventData)
        {
            if (clusterClient == null || session.WorldId == null)
                return; // Orleans not available or no world context

            try
            {
                // Get world info to find narrative ID
                var worldGrain = clusterClient.GetGrain<Aetherium.Server.MultiWorld.IWorldGrain>(session.WorldId);
                var worldInfo = await worldGrain.GetInfoAsync();
                
                if (worldInfo?.NarrativeId == null)
                    return; // No narrative associated with this world

                var consequenceEngine = new NarrativeConsequenceEngine(clusterClient);
                var narrativeStateScope = worldInfo.Metadata?.TryGetValue("NarrativeStateScope", out var scopeObj) == true 
                    ? scopeObj?.ToString() 
                    : "shared";

                await consequenceEngine.ProcessEventAsync(
                    session.WorldId,
                    worldInfo.NarrativeId,
                    eventType,
                    eventData,
                    narrativeStateScope);
            }
            catch (Exception ex)
            {
                // Non-breaking: log but don't fail the interaction
                Console.WriteLine($"[GameHub] Failed to process narrative event: {ex.Message}");
            }
        }

        public override async Task OnConnectedAsync()
        {
            Console.WriteLine($"Client connected: {Context.ConnectionId}");

            // Select world builder (env-flag gated)
            WorldBuilder builder;
            var audioTest = Environment.GetEnvironmentVariable("AUDIO_TEST");
            if (string.Equals(audioTest, "1", StringComparison.OrdinalIgnoreCase))
                builder = new AudioTestWorldBuilder();
            else
                builder = new FovDiagnosticWorldBuilder("open_space");

            // DIAGNOSTIC - Only write in UI self-test mode
            var testMode = Environment.GetEnvironmentVariable("UI_SELFTEST_MODE") == "1";
            if (testMode)
            {
                try {
                    var diagFile = Path.Combine(Environment.CurrentDirectory, "..", ".ui-test", "gamehub_diagnostics.txt");
                    var dir = Path.GetDirectoryName(diagFile);
                    if (dir != null)
                    {
                        Directory.CreateDirectory(dir);
                        File.WriteAllText(diagFile, $"GameHub creating session with builder: {builder.GetType().Name}\nAUDIO_TEST={audioTest}\n");
                    }
                } catch { /* ignore */ }
            }

        // Create a new game session for this client
        var session = sessionManager.CreateSession(Context.ConnectionId, builder);

        // Register session with management grain
        try
        {
            var managementGrain = GetManagementGrain();
            if (managementGrain != null)
            {
                await managementGrain.RegisterSessionAsync(session.SessionId, Context.ConnectionId);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[GameHub] Failed to register session with grain: {ex.Message}");
            // Continue anyway - grain registration is optional
        }

        // Send initial game state (without world coordinates)
            var initialState = new GameStateDto
            {
                PlayerId = session.SessionId,
                // DO NOT send PlayerLocation - client should not know absolute world coordinates
                PlayerHeading = session.Heading.ToDto()
            };

            await Clients.Caller.SendAsync("ReceiveGameState", initialState);

            // Send initial perception
            var perception = session.GetPerception();
            await Clients.Caller.SendAsync("ReceivePerceptionUpdate", perception);

            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            Console.WriteLine($"Client disconnected: {Context.ConnectionId}");
            
            // Get session ID before removing
            var session = sessionManager.GetSession(Context.ConnectionId);
            var sessionId = session?.SessionId;

            // Unregister from management grain
            if (sessionId != null)
            {
                try
                {
                    var managementGrain = GetManagementGrain();
                    if (managementGrain != null)
                    {
                        await managementGrain.UnregisterSessionAsync(sessionId);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[GameHub] Failed to unregister session from grain: {ex.Message}");
                }
            }

            sessionManager.RemoveSession(Context.ConnectionId);
            await base.OnDisconnectedAsync(exception);
        }

        /// <summary>
        /// Moves the player in the specified direction.
        /// </summary>
        [Obsolete("Use ExecuteTool(\"move\", args) instead. This method will be removed in a future version.")]
        public async Task MovePlayer(Aetherium.Model.RelativeDirection direction, int distance)
        {
            var session = sessionManager.GetSession(Context.ConnectionId);
            if (session == null)
                return;

            session.MoveView(direction, distance);

            // Send updated perception
            var perception = session.GetPerception();
            await Clients.Caller.SendAsync("ReceivePerceptionUpdate", perception);
        }

        /// <summary>
        /// Rotates the player's view.
        /// </summary>
        [Obsolete("Use ExecuteTool(\"rotate\", args) instead. This method will be removed in a future version.")]
        public async Task RotatePlayer(bool clockwise)
        {
            var session = sessionManager.GetSession(Context.ConnectionId);
            if (session == null)
                return;

            session.RotateView(clockwise);

            // Send updated perception
            var perception = session.GetPerception();
            await Clients.Caller.SendAsync("ReceivePerceptionUpdate", perception);
        }

        /// <summary>
        /// Rotates the player by a specific number of degrees.
        /// Positive values rotate clockwise, negative values rotate counter-clockwise.
        /// </summary>
        [Obsolete("Use ExecuteTool(\"rotate\", args) instead. This method will be removed in a future version.")]
        public async Task RotatePlayerDegrees(int degrees)
        {
            var session = sessionManager.GetSession(Context.ConnectionId);
            if (session == null)
                return;

            session.RotateView(degrees);

            // Send updated perception
            var perception = session.GetPerception();
            await Clients.Caller.SendAsync("ReceivePerceptionUpdate", perception);
        }

        /// <summary>
        /// Toggles directional vision mode on or off.
        /// When enabled, the player can only see within a forward-facing cone.
        /// </summary>
        [Obsolete("Use ExecuteTool(\"toggledirectionalvision\", args) instead. This method will be removed in a future version.")]
        public async Task ToggleDirectionalVision()
        {
            var session = sessionManager.GetSession(Context.ConnectionId);
            if (session == null)
                return;

            session.DirectionalVisionMode = !session.DirectionalVisionMode;
            Console.WriteLine($"Directional vision mode: {(session.DirectionalVisionMode ? "ON" : "OFF")}");

            // Send updated perception with new vision mode
            var perception = session.GetPerception();
            await Clients.Caller.SendAsync("ReceivePerceptionUpdate", perception);
        }

        [Obsolete("Use ExecuteTool(\"changelevel\", args) instead. This method will be removed in a future version.")]
        public async Task ChangeLevel(int deltaZ)
        {
            var session = sessionManager.GetSession(Context.ConnectionId);
            if (session == null)
                return;

            session.ChangeLevel(deltaZ);

            // Send updated perception
            var perception = session.GetPerception();
            await Clients.Caller.SendAsync("ReceivePerceptionUpdate", perception);
        }

        [Obsolete("Use ExecuteTool(\"jumptolocation\", args) instead. This method will be removed in a future version.")]
        public async Task JumpToRandomLocation()
        {
            var session = sessionManager.GetSession(Context.ConnectionId);
            if (session == null)
                return;

            session.JumpToRandomLocation();

            // Send updated perception
            var perception = session.GetPerception();
            await Clients.Caller.SendAsync("ReceivePerceptionUpdate", perception);
        }

        [Obsolete("Use ExecuteTool(\"pickup\", args) instead. This method will be removed in a future version.")]
        public async Task<InteractionResultDto> Pickup(string targetEntityId)
        {
            var session = sessionManager.GetSession(Context.ConnectionId);
            if (session == null)
                return new InteractionResultDto { Success = false, Reason = "No session" };

            var result = interactionSystem.TryPickup(session, targetEntityId);
            
            // Process narrative consequences if successful
            if (result.Success && clusterClient != null && session.WorldId != null)
            {
                await ProcessNarrativeEventAsync(session, "item_collected", new Dictionary<string, object>
                {
                    ["itemId"] = targetEntityId
                });
            }
            
            var perception = session.GetPerception();
            await Clients.Caller.SendAsync("ReceivePerceptionUpdate", perception);
            return new InteractionResultDto { Success = result.Success, Reason = result.Reason };
        }

        [Obsolete("Use ExecuteTool(\"drop\", args) instead. This method will be removed in a future version.")]
        public async Task<InteractionResultDto> Drop(string itemEntityId)
        {
            var session = sessionManager.GetSession(Context.ConnectionId);
            if (session == null)
                return new InteractionResultDto { Success = false, Reason = "No session" };

            var result = interactionSystem.TryDrop(session, itemEntityId);
            var perception = session.GetPerception();
            await Clients.Caller.SendAsync("ReceivePerceptionUpdate", perception);
            return new InteractionResultDto { Success = result.Success, Reason = result.Reason };
        }

        [Obsolete("Use ExecuteTool(\"use\", args) instead. This method will be removed in a future version.")]
        public async Task<InteractionResultDto> Use(string itemEntityId, string onEntityId, string? usageId = null)
        {
            var session = sessionManager.GetSession(Context.ConnectionId);
            if (session == null)
                return new InteractionResultDto { Success = false, Reason = "No session" };

            var result = interactionSystem.TryUse(session, itemEntityId, onEntityId, usageId);
            
            // Handle reactive disambiguation: if options are returned, return them in the DTO
            if (result.Options != null && result.Options.Count > 0)
            {
                return new InteractionResultDto 
                { 
                    Success = false, 
                    Reason = result.Reason,
                    Options = result.Options.Select(opt => new UsageOptionDto
                    {
                        UsageId = opt.UsageId,
                        Label = opt.Label,
                        Description = opt.Description
                    }).ToList()
                };
            }
            
            // Process narrative consequences if successful
            if (result.Success && clusterClient != null && session.WorldId != null)
            {
                await ProcessNarrativeEventAsync(session, "item_used", new Dictionary<string, object>
                {
                    ["itemId"] = itemEntityId,
                    ["targetId"] = onEntityId
                });
            }
            
            var perception = session.GetPerception();
            await Clients.Caller.SendAsync("ReceivePerceptionUpdate", perception);
            return new InteractionResultDto { Success = result.Success, Reason = result.Reason };
        }

        [Obsolete("Use ExecuteTool(\"open\", args) instead. This method will be removed in a future version.")]
        public async Task<InteractionResultDto> Open(string targetEntityId)
        {
            var session = sessionManager.GetSession(Context.ConnectionId);
            if (session == null)
                return new InteractionResultDto { Success = false, Reason = "No session" };

            var result = interactionSystem.TryOpen(session, targetEntityId);
            
            // Process narrative consequences if successful
            if (result.Success && clusterClient != null && session.WorldId != null)
            {
                await ProcessNarrativeEventAsync(session, "door_opened", new Dictionary<string, object>
                {
                    ["doorId"] = targetEntityId
                });
            }
            
            var perception = session.GetPerception();
            await Clients.Caller.SendAsync("ReceivePerceptionUpdate", perception);
            return new InteractionResultDto { Success = result.Success, Reason = result.Reason };
        }

        [Obsolete("Use ExecuteTool(\"close\", args) instead. This method will be removed in a future version.")]
        public async Task<InteractionResultDto> Close(string targetEntityId)
        {
            var session = sessionManager.GetSession(Context.ConnectionId);
            if (session == null)
                return new InteractionResultDto { Success = false, Reason = "No session" };

            var result = interactionSystem.TryClose(session, targetEntityId);
            
            // Process narrative consequences if successful
            if (result.Success && clusterClient != null && session.WorldId != null)
            {
                await ProcessNarrativeEventAsync(session, "door_closed", new Dictionary<string, object>
                {
                    ["doorId"] = targetEntityId
                });
            }
            
            var perception = session.GetPerception();
            await Clients.Caller.SendAsync("ReceivePerceptionUpdate", perception);
            return new InteractionResultDto { Success = result.Success, Reason = result.Reason };
        }

        public async Task<InteractionResultDto> UsePortal(string portalEntityId)
        {
            var session = sessionManager.GetSession(Context.ConnectionId);
            if (session == null)
                return new InteractionResultDto { Success = false, Reason = "No session" };

            if (session.Player == null || session.ViewLocation == null)
                return new InteractionResultDto { Success = false, Reason = "No player or view location" };

            if (!session.World.Entities.TryGetValue(portalEntityId, out var portalEntityObj))
                return new InteractionResultDto { Success = false, Reason = "Portal not found" };

            var portalComponent = portalEntityObj.Get<PortalComponent>();
            if (portalComponent == null)
                return new InteractionResultDto { Success = false, Reason = "Not a portal" };

            // Check if portal is active
            if (!portalComponent.IsActive)
                return new InteractionResultDto { Success = false, Reason = "Portal is not active" };

            // Check if player is at portal location
            var portalLoc = portalEntityObj.Get<WorldLocation>();
            if (portalLoc == null || portalLoc != session.ViewLocation)
                return new InteractionResultDto { Success = false, Reason = "Not at portal location" };

            // Check activation requirements (simplified - full implementation would query meta-progression grain)
            if (!string.IsNullOrEmpty(portalComponent.Activation))
            {
                // TODO: Check activation requirements via meta-progression grain
                // For now, only allow portals with no activation requirements or "unlocked"
                if (!portalComponent.Activation.Equals("unlocked", StringComparison.OrdinalIgnoreCase))
                {
                    return new InteractionResultDto { Success = false, Reason = "Portal activation requirement not met" };
                }
            }

            // Resolve portal target if needed
            string? targetWorldId = portalComponent.TargetWorldId;
            string? targetMapId = portalComponent.TargetMapId;

            if (string.IsNullOrEmpty(targetWorldId) || string.IsNullOrEmpty(targetMapId))
            {
                // Need to resolve via cluster grain
                if (clusterClient == null || session.WorldId == null)
                    return new InteractionResultDto { Success = false, Reason = "Cluster resolution not available" };

                var worldGrain = clusterClient.GetGrain<IWorldGrain>(session.WorldId);
                var worldInfo = await worldGrain.GetInfoAsync();
                
                if (worldInfo == null || string.IsNullOrEmpty(worldInfo.ClusterId))
                    return new InteractionResultDto { Success = false, Reason = "World not part of a cluster" };

                var clusterGrain = clusterClient.GetGrain<IClusterGrain>(worldInfo.ClusterId);
                var (resolvedWorldId, resolvedMapId) = await clusterGrain.ResolvePortalTargetAsync(portalComponent.PortalId, portalComponent.TargetTag);
                
                if (string.IsNullOrEmpty(resolvedWorldId) || string.IsNullOrEmpty(resolvedMapId))
                    return new InteractionResultDto { Success = false, Reason = "Could not resolve portal target" };
                
                // Cache resolved target in portal component for efficiency
                portalComponent.TargetWorldId = resolvedWorldId;
                portalComponent.TargetMapId = resolvedMapId;
                
                targetWorldId = resolvedWorldId;
                targetMapId = resolvedMapId;
            }

            // Transport player to target location
            if (clusterClient == null || session.WorldId == null)
                return new InteractionResultDto { Success = false, Reason = "Orleans not available for world transport" };

            try
            {
                // For now, if target is in same world, move player between maps
                if (targetWorldId == session.WorldId)
                {
                    var worldGrain = clusterClient.GetGrain<IWorldGrain>(targetWorldId);
                    var moved = await worldGrain.MovePlayerToMapAsync(session.SessionId, targetMapId);
                    
                    if (!moved)
                        return new InteractionResultDto { Success = false, Reason = "Failed to move player to target map" };
                }
                else
                {
                    // Cross-world transport - remove from current world, add to target world
                    var currentWorldGrain = clusterClient.GetGrain<IWorldGrain>(session.WorldId);
                    await currentWorldGrain.RemovePlayerAsync(session.SessionId);
                    
                    var targetWorldGrain = clusterClient.GetGrain<IWorldGrain>(targetWorldId);
                    var added = await targetWorldGrain.AddPlayerAsync(session.SessionId, targetMapId);
                    
                    if (!added)
                        return new InteractionResultDto { Success = false, Reason = "Failed to add player to target world" };
                    
                    // Update session world ID
                    session.WorldId = targetWorldId;
                    // TODO: Load new world/map into session
                }

            // Emit arrival event for narrative system
            if (clusterClient != null && session.WorldId != null)
            {
                var eventData = new Dictionary<string, object>
                {
                    ["worldId"] = targetWorldId ?? "",
                    ["mapId"] = targetMapId ?? "",
                    ["playerId"] = session.SessionId
                };
                
                await ProcessNarrativeEventAsync(session, "player_arrived", eventData);
                
                // Record discovery in meta-progression
                if (!string.IsNullOrEmpty(targetWorldId) && !string.IsNullOrEmpty(targetMapId))
                {
                    try
                    {
                        // Get world and map info for discovery metadata
                        var worldGrain = clusterClient.GetGrain<IWorldGrain>(targetWorldId);
                        var worldInfo = await worldGrain.GetInfoAsync();
                        
                        string? worldTemplate = null;
                        List<string>? tags = null;
                        
                        if (worldInfo?.Metadata != null)
                        {
                            if (worldInfo.Metadata.TryGetValue("generatorType", out var genType))
                            {
                                worldTemplate = genType?.ToString();
                            }
                            
                            if (worldInfo.Metadata.TryGetValue("tags", out var tagsObj) && tagsObj is List<string> tagList)
                            {
                                tags = tagList;
                            }
                        }
                        
                        // Record discovery (use session ID as player ID for now)
                        var metaProgGrain = clusterClient.GetGrain<MetaProgression.IMetaProgressionGrain>(session.SessionId);
                        await metaProgGrain.RecordDiscoveryAsync(targetWorldId, targetMapId, worldTemplate, tags);
                    }
                    catch (Exception ex)
                    {
                        // Non-breaking: log but don't fail portal transport
                        Console.WriteLine($"[GameHub] Failed to record discovery: {ex.Message}");
                    }
                }
            }

                // Send updated perception to client
                var perception = session.GetPerception();
                await Clients.Caller.SendAsync("ReceivePerceptionUpdate", perception);
                
                return new InteractionResultDto { Success = true, Reason = $"Transported to {targetWorldId}:{targetMapId}" };
            }
            catch (Exception ex)
            {
                return new InteractionResultDto { Success = false, Reason = $"Portal transport failed: {ex.Message}" };
            }
        }

        [Obsolete("Use ExecuteTool(\"setlightingmode\", args) instead. This method will be removed in a future version.")]
        public async Task SetLightingMode(LightingMode mode)
        {
            var session = sessionManager.GetSession(Context.ConnectionId);
            if (session == null)
                return;

            session.CurrentLightingMode = mode;

            // Send updated perception with new lighting mode
            var perception = session.GetPerception();
            await Clients.Caller.SendAsync("ReceivePerceptionUpdate", perception);
        }

        [Obsolete("Use ExecuteTool(\"setvisionmode\", args) instead. This method will be removed in a future version.")]
        public async Task SetVisionMode(VisionMode mode)
        {
            var session = sessionManager.GetSession(Context.ConnectionId);
            if (session == null)
                return;

            session.CurrentVisionMode = mode;

            // Send updated perception with new vision mode
            var perception = session.GetPerception();
            await Clients.Caller.SendAsync("ReceivePerceptionUpdate", perception);
        }

        // ============================================================
        // Multi-World Management Methods
        // ============================================================

        /// <summary>
        /// Lists all available game worlds.
        /// </summary>
        public async Task<List<WorldInfo>> ListWorlds()
        {
            try
            {
                var managementGrain = GetManagementGrain();
                if (managementGrain == null)
                    return new List<WorldInfo>();

                return await managementGrain.ListWorldsAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[GameHub] Error listing worlds: {ex.Message}");
                return new List<WorldInfo>();
            }
        }

        /// <summary>
        /// Gets detailed information about a specific world.
        /// </summary>
        public async Task<WorldInfo?> GetWorldInfo(string worldId)
        {
            try
            {
                var managementGrain = GetManagementGrain();
                if (managementGrain == null)
                    return null;

                return await managementGrain.GetWorldInfoAsync(worldId);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[GameHub] Error getting world info: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Joins a specific world by ID.
        /// If the client is already in a world, they will be moved to the new world.
        /// </summary>
        public async Task<OperationResult> JoinWorld(string worldId)
        {
            try
            {
                // Remove existing session if any
                var existingSession = sessionManager.GetSession(Context.ConnectionId);
                if (existingSession != null)
                {
                    sessionManager.RemoveSession(Context.ConnectionId);
                }

                var managementGrain = GetManagementGrain();
                if (managementGrain == null)
                {
                    return OperationResult.Error("Orleans is not available");
                }

                // Get world grain
                if (clusterClient == null)
                {
                    return OperationResult.Error("Cluster client not available");
                }

                var worldGrain = clusterClient.GetGrain<IWorldGrain>(worldId);
                var worldState = await worldGrain.GetStateAsync();

                if (worldState != WorldState.Active)
                {
                    return OperationResult.Error($"World {worldId} is not available (state: {worldState})");
                }

                // Get the world's primary map (for now, assume map ID = worldId)
                // TODO: Support multiple maps per world
                var mapGrain = clusterClient.GetGrain<IGameMapGrain>(worldId);
                var worldData = await mapGrain.GetWorldAsync();

                if (worldData == null)
                {
                    return OperationResult.Error($"World {worldId} has no active map");
                }

                // TODO: Deserialize worldData when serialization is implemented
                return OperationResult.Error("Joining worlds via GameHub is not yet supported.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[GameHub] Error joining world: {ex.Message}");
                return OperationResult.Error($"Failed to join world: {ex.Message}");
            }
        }
        
        // ============================================================
        // Unified Tool Execution API
        // ============================================================
        
        /// <summary>
        /// Executes a tool by ID with the specified arguments.
        /// This is the new unified API for all game actions.
        /// </summary>
        public async Task<ToolExecutionResultDto> ExecuteTool(string toolId, Dictionary<string, object> args)
        {
            try
            {
                if (toolRegistry == null)
                {
                    return new ToolExecutionResultDto 
                    { 
                        Success = false, 
                        Message = "Tool registry not available" 
                    };
                }
                
                var session = sessionManager.GetSession(Context.ConnectionId);
                if (session == null)
                {
                    return new ToolExecutionResultDto 
                    { 
                        Success = false, 
                        Message = "No active session" 
                    };
                }
                
                var tool = toolRegistry.GetTool(toolId);
                if (tool == null)
                {
                    return new ToolExecutionResultDto 
                    { 
                        Success = false, 
                        Message = $"Tool not found: {toolId}" 
                    };
                }
                
                // Create execution context for player
                var context = new Aetherium.Server.Agents.Tools.ToolExecutionContext
                {
                    SessionId = session.SessionId,
                    ConnectionId = Context.ConnectionId,
                    Session = session,
                    InteractionSystem = interactionSystem,
                    ManagementGrain = GetManagementGrain(),
                    GrantedCapabilities = new HashSet<string> 
                    { 
                        // Players get full access to player-level capabilities
                        "basic_movement", "inventory_access", "interaction", "vision" 
                    },
                    ServiceProvider = Context.GetHttpContext()?.RequestServices ?? throw new InvalidOperationException("Service provider not available")
                };
                
                // Execute the tool
                var result = await tool.ExecuteAsync(context, args);
                
                // Process narrative consequences for interaction tools
                if (result.Success && clusterClient != null && session.WorldId != null)
                {
                    if (toolId == "open" && args.TryGetValue("targetEntityId", out var openTargetId))
                    {
                        await ProcessNarrativeEventAsync(session, "door_opened", new Dictionary<string, object>
                        {
                            ["doorId"] = openTargetId?.ToString() ?? string.Empty
                        });
                    }
                    else if (toolId == "close" && args.TryGetValue("targetEntityId", out var closeTargetId))
                    {
                        await ProcessNarrativeEventAsync(session, "door_closed", new Dictionary<string, object>
                        {
                            ["doorId"] = closeTargetId?.ToString() ?? string.Empty
                        });
                    }
                }
                
                // Send updated perception to client
                var perception = session.GetPerception();
                await Clients.Caller.SendAsync("ReceivePerceptionUpdate", perception);
                
                return result.ToDto();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[GameHub] Error executing tool {toolId}: {ex.Message}");
                return new ToolExecutionResultDto 
                { 
                    Success = false, 
                    Message = $"Error executing tool: {ex.Message}" 
                };
            }
        }
        
        /// <summary>
        /// Lists all available tools for the current player.
        /// </summary>
        public async Task<List<ToolInfoDto>> ListAvailableTools()
        {
            try
            {
                if (toolRegistry == null)
                    return new List<ToolInfoDto>();
                
                // Use Player profile (for all game characters - NPCs and human players)
                var profile = Aetherium.Server.Agents.Tools.AgentToolProfile.Player;
                
                var tools = toolRegistry.GetToolsForProfile(profile)
                    .Select(t => t.ToDto())
                    .ToList();
                
                return await Task.FromResult(tools);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[GameHub] Error listing tools: {ex.Message}");
                return new List<ToolInfoDto>();
            }
        }
    }
}


