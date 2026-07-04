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
using Aetherium.Server.Narrative;
using Aetherium.Server.Narrative.Consequence;
using Aetherium.Server.Narrative.State;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Orleans;

namespace Aetherium.Server
{
    // Policy-gated rather than attribute-bare: the "GameClient" policy is registered
    // unconditionally in Program.cs but adapts to deployment — RequireAuthenticatedUser
    // when Azure AD B2C is configured, allow-anonymous otherwise. This means production
    // deployments with B2C automatically enforce auth; dev runs without B2C still work.
    [Authorize(Policy = "GameClient")]
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

            // Phase 1 hub-grain bridge: if the client included ?worldId= in the SignalR
            // connection query string, auto-join that world now. This swaps the session's
            // World from the legacy private FovDiagnosticWorldBuilder world to one
            // hydrated from the grain's snapshot. Failure here is logged but doesn't
            // refuse the connection — the client is left in the legacy private world.
            try
            {
                var http = Context.GetHttpContext();
                var requestedWorldId = http?.Request.Query["worldId"].ToString();
                if (!string.IsNullOrEmpty(requestedWorldId))
                {
                    var requestedMapId = http?.Request.Query["mapId"].ToString();
                    var joinResult = await JoinWorld(
                        requestedWorldId!,
                        string.IsNullOrEmpty(requestedMapId) ? null : requestedMapId);
                    if (!joinResult.Success)
                        Console.WriteLine($"[GameHub] Auto-join '{requestedWorldId}' failed: {joinResult.Reason}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[GameHub] Auto-join from query string threw: {ex.Message}");
            }

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

            // Phase 2c: if the session was grain-bound, tell the map grain to drop
            // the player Character and emit an EntityRemovedDelta so other joined
            // sessions see them leave. Phase-2 player persistence (deferred) will
            // replace this with a snapshot-and-detach flow that lets players resume.
            if (sessionId != null && session?.MapId != null && clusterClient != null)
            {
                try
                {
                    var mapGrain = clusterClient.GetGrain<IGameMapGrain>(session.MapId);
                    await mapGrain.LeavePlayerAsync(sessionId);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[GameHub] Failed to leave map grain: {ex.Message}");
                }
            }

            sessionManager.RemoveSession(Context.ConnectionId);
            await base.OnDisconnectedAsync(exception);
        }

        // ----------------------------------------------------------------
        // Phase 2d: the per-verb [Obsolete] hub methods (MovePlayer, Rotate*,
        // Pickup, Drop, Use, Open, Close, ChangeLevel, JumpToRandomLocation,
        // ToggleDirectionalVision, SetLightingMode, SetVisionMode) are gone.
        // Clients use ExecuteTool("move", args) etc. for every gameplay verb.
        // The narrative-consequence emission for door/item events that used to
        // live in those obsolete hub methods is preserved in ExecuteTool's
        // post-dispatch branch below.
        // ----------------------------------------------------------------

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

        // (SetLightingMode / SetVisionMode also removed in phase 2d; clients
        // use ExecuteTool("setlightingmode", args) and ExecuteTool("setvisionmode", args).)

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
        /// Binds the caller's session to the given world by hydrating its
        /// <see cref="GameSession.World"/> from a snapshot served by
        /// <see cref="IGameMapGrain"/>.
        ///
        /// <para>
        /// PHASE 1 semantics: the resulting World is independent per-session.
        /// Two clients calling JoinWorld with the same id see identical initial
        /// layouts and identical entity IDs, but mutations are local — picking
        /// up an item in one session is not visible in the other. Live shared
        /// mutation is deferred to phase 2.
        /// </para>
        /// </summary>
        public async Task<JoinWorldResult> JoinWorld(string worldId, string? mapId = null)
        {
            try
            {
                if (string.IsNullOrEmpty(worldId))
                    return JoinWorldResult.Fail("worldId required");

                if (clusterClient == null)
                    return JoinWorldResult.Fail("Cluster client not available");

                var session = sessionManager.GetSession(Context.ConnectionId);
                if (session == null)
                    return JoinWorldResult.Fail("No active session");

                // Resolve world + state.
                var worldGrain = clusterClient.GetGrain<IWorldGrain>(worldId);
                var worldInfo = await worldGrain.GetInfoAsync();
                if (worldInfo == null)
                    return JoinWorldResult.Fail("World not found");

                var worldState = await worldGrain.GetStateAsync();
                if (worldState != WorldState.Active)
                    return JoinWorldResult.Fail($"World is not active (state: {worldState})");

                // Resolve map.
                string? resolvedMapId = mapId;
                if (string.IsNullOrEmpty(resolvedMapId))
                {
                    resolvedMapId = worldInfo.MapIds?.FirstOrDefault();
                    if (string.IsNullOrEmpty(resolvedMapId))
                        return JoinWorldResult.Fail("World has no maps");
                }

                // Register on the map grain and grab the spawn assignment.
                var mapGrain = clusterClient.GetGrain<IGameMapGrain>(resolvedMapId);
                var joinResult = await mapGrain.JoinPlayerAsync(session.SessionId);
                if (!joinResult.Success)
                    return JoinWorldResult.Fail(joinResult.Reason ?? "Join failed");

                // Fetch a snapshot of the canonical world, omitting the joiner's own
                // Character so they don't see themselves twice. Their local session
                // creates a fresh Player on hydration with EntityId == SessionId.
                var snapshot = await mapGrain.GetWorldSnapshotForJoinerAsync(session.SessionId);

                var generatorRegistry = Context.GetHttpContext()?.RequestServices
                    .GetService<Aetherium.WorldGen.MapGeneratorRegistry>();
                if (generatorRegistry == null)
                    return JoinWorldResult.Fail("Generator registry not available");

                var builder = new Aetherium.WorldBuilders.SnapshotWorldBuilder(snapshot, generatorRegistry);
                sessionManager.ReplaceSessionWorld(
                    session, builder, worldId, resolvedMapId, joinResult.SpawnLocation());

                // Phase 2c: swap the session's mutation gateway to a grain-routed one.
                // From here on, every gameplay verb on this session's tools dispatches
                // to the grain, which mutates canonical state and pushes deltas back
                // to every joined session via NotifyMapMutationAsync.
                session.Gateway = new Aetherium.Server.MultiWorld.GrainMutationGateway(
                    clusterClient, resolvedMapId, session.SessionId);

                // Push the first perception of the new world to the caller.
                var perception = session.GetPerception();
                await Clients.Caller.SendAsync("ReceivePerceptionUpdate", perception);

                // Emit an arrival event so travel_to / reach_location objectives that target this
                // world can complete on join. Previously only UsePortal emitted player_arrived, so
                // quests whose destination is the world a player joins into never progressed.
                await ProcessNarrativeEventAsync(session, "player_arrived", new Dictionary<string, object>
                {
                    ["worldId"] = worldId,
                    ["mapId"] = resolvedMapId!,
                    ["playerId"] = session.SessionId
                });

                return JoinWorldResult.Ok(worldId, resolvedMapId, joinResult.SpawnLocation());
            }
            catch (Exception ex)
            {
                // Log server-side detail; return sanitized reason to the client.
                Console.WriteLine($"[GameHub] JoinWorld({worldId}) threw: {ex}");
                return JoinWorldResult.Fail("Join failed");
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

                // Enforce the Player profile at the hub boundary so a missing or
                // forgotten in-tool capability check can't silently grant world-builder
                // or admin tools to a human client. The tool itself still re-checks via
                // HasCapability for defense in depth.
                var profile = Aetherium.Server.Agents.Tools.AgentToolProfile.Player;
                if (!profile.IsToolAllowed(tool))
                {
                    return new ToolExecutionResultDto
                    {
                        Success = false,
                        Message = $"Tool '{toolId}' is not available for this profile"
                    };
                }

                // Create execution context for player. The MutationGateway is the contract
                // tools mutate through.
                //   * Phase 2a: LocalMutationGateway (in-process, session-local mutation)
                //   * Phase 2c: when the session is grain-bound (via JoinWorld),
                //     session.Gateway is a GrainMutationGateway and we use it. The grain
                //     applies the mutation to canonical state and the host-side delta
                //     broker fans perception updates to every joined session.
                var gateway = session.Gateway
                    ?? (Aetherium.Server.MultiWorld.IMapMutationGateway)new Aetherium.Server.MultiWorld.LocalMutationGateway(session, interactionSystem);

                var context = new Aetherium.Server.Agents.Tools.ToolExecutionContext
                {
                    SessionId = session.SessionId,
                    ConnectionId = Context.ConnectionId,
                    Session = session,
                    MutationGateway = gateway,
                    ManagementGrain = GetManagementGrain(),
                    // Capabilities come from the enforced profile, not a hardcoded literal,
                    // so any future profile changes flow through automatically.
                    GrantedCapabilities = new HashSet<string>(profile.GrantedCapabilities),
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
                    else if (toolId == "pickup" && args.TryGetValue("targetEntityId", out var pickupTargetId))
                    {
                        var itemId = pickupTargetId?.ToString() ?? string.Empty;
                        var eventData = new Dictionary<string, object> { ["itemId"] = itemId };
                        // The consequence engine keys collection quests off itemType.
                        if (session.World?.Entities.TryGetValue(itemId, out var pickedUp) == true)
                            eventData["itemType"] = pickedUp.GetType().Name;
                        await ProcessNarrativeEventAsync(session, "item_collected", eventData);
                    }
                    else if (toolId == "use" && args.TryGetValue("itemEntityId", out var usedItemId))
                    {
                        await ProcessNarrativeEventAsync(session, "item_used", new Dictionary<string, object>
                        {
                            ["itemId"] = usedItemId?.ToString() ?? string.Empty,
                            ["targetId"] = args.TryGetValue("onEntityId", out var usedOnId)
                                ? usedOnId?.ToString() ?? string.Empty
                                : string.Empty
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
                // Log full detail server-side; return an opaque message to the client so
                // internal exception text (paths, types, grain identifiers) isn't leaked.
                Console.WriteLine($"[GameHub] Error executing tool {toolId}: {ex}");
                return new ToolExecutionResultDto
                {
                    Success = false,
                    Message = "Tool execution failed"
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

        // ============================================================
        // Quest Surface (P3-2)
        // ============================================================

        /// <summary>
        /// Resolves the narrative-state grain governing the caller's current world, or null when
        /// Orleans is unavailable, there is no session/world, or the world has no narrative.
        /// </summary>
        private async Task<INarrativeStateGrain?> ResolveNarrativeStateGrainAsync()
        {
            if (clusterClient == null)
                return null;

            var session = sessionManager.GetSession(Context.ConnectionId);
            if (session == null)
                return null;

            return await NarrativeStateResolver.ResolveForWorldAsync(clusterClient, session.WorldId);
        }

        /// <summary>
        /// Lists quests the caller can currently start (prerequisites met, not active/completed)
        /// in their current world's narrative.
        /// </summary>
        public async Task<List<QuestSummaryDto>> ListAvailableQuests()
        {
            try
            {
                var grain = await ResolveNarrativeStateGrainAsync();
                if (grain == null)
                    return new List<QuestSummaryDto>();

                var available = await grain.GetAvailableQuestsAsync();
                return available.Select(q => ToSummaryDto(q, isActive: false, isCompleted: false, state: null)).ToList();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[GameHub] Error listing available quests: {ex.Message}");
                return new List<QuestSummaryDto>();
            }
        }

        /// <summary>
        /// Accepts (activates) a quest for the caller's current world. Returns true when the quest
        /// was started; false when unknown, already active/completed, prerequisites unmet, or no
        /// narrative context is available.
        /// </summary>
        public async Task<bool> AcceptQuest(string questId)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(questId))
                    return false;

                var grain = await ResolveNarrativeStateGrainAsync();
                if (grain == null)
                    return false;

                return await grain.StartQuestAsync(questId);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[GameHub] Error accepting quest '{questId}': {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Returns the caller's quest log: active quests with per-objective progress, plus the set
        /// of completed quest IDs.
        /// </summary>
        public async Task<QuestLogDto> GetQuestLog()
        {
            var log = new QuestLogDto();
            try
            {
                var grain = await ResolveNarrativeStateGrainAsync();
                if (grain == null)
                    return log;

                var state = await grain.GetStateAsync();
                var active = await grain.GetActiveQuestsAsync();

                log.Active = active.Select(q => ToSummaryDto(q, isActive: true, isCompleted: false, state)).ToList();
                log.Completed = state?.CompletedQuestIds.ToList() ?? new List<string>();
                return log;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[GameHub] Error getting quest log: {ex.Message}");
                return log;
            }
        }

        /// <summary>
        /// Maps a server-side quest definition onto the standalone player-facing DTO, filling in
        /// per-objective completion and progress from narrative state when provided.
        /// </summary>
        private static QuestSummaryDto ToSummaryDto(
            Aetherium.Server.Narrative.QuestDefinition quest, bool isActive, bool isCompleted, NarrativeState? state)
        {
            var dto = new QuestSummaryDto
            {
                QuestId = quest.QuestId,
                Title = quest.Title,
                Description = quest.Description,
                IsActive = isActive,
                IsCompleted = isCompleted
            };

            HashSet<string>? completedObjectives = null;
            Dictionary<string, int>? progress = null;
            state?.CompletedObjectives.TryGetValue(quest.QuestId, out completedObjectives);
            state?.ObjectiveProgress.TryGetValue(quest.QuestId, out progress);

            foreach (var objective in quest.Objectives ?? new List<Aetherium.Server.Narrative.QuestObjective>())
            {
                int required = QuestObjectiveRequiredCount(objective);
                bool done = completedObjectives != null && completedObjectives.Contains(objective.ObjectiveId);
                int current = done
                    ? required
                    : (progress != null && progress.TryGetValue(objective.ObjectiveId, out var p) ? p : 0);

                dto.Objectives.Add(new QuestObjectiveDto
                {
                    ObjectiveId = objective.ObjectiveId,
                    Type = objective.Type,
                    Completed = done,
                    Progress = current,
                    Required = required
                });
            }

            return dto;
        }

        /// <summary>
        /// Mirrors NarrativeStateGrain's required-count parsing for count-based objectives so the
        /// log surfaces the same target the grain enforces. Defaults to 1.
        /// </summary>
        private static int QuestObjectiveRequiredCount(Aetherium.Server.Narrative.QuestObjective objective)
        {
            foreach (var key in new[] { "requiredCount", "count", "requiredQuantity", "quantity" })
            {
                if (objective.Parameters != null &&
                    objective.Parameters.TryGetValue(key, out var v))
                {
                    switch (v)
                    {
                        case int i when i > 0: return i;
                        case long l when l > 0: return (int)l;
                        case double d when d > 0: return (int)d;
                        default:
                            if (int.TryParse(v?.ToString(), out var n) && n > 0)
                                return n;
                            break;
                    }
                }
            }
            return 1;
        }
    }
}


