using System;
using System.CommandLine;
using System.IO;
using System.Threading.Tasks;
using System.Text.Json;
using AgentCLI;
using ConsoleGameServer.MultiWorld;
using ConsoleGameServer.Narrative;
using ConsoleGameServer.Agents;

namespace AgentCLI
{
    /// <summary>
    /// CLI tool for managing agents in the game server.
    /// Commands:
    ///   - join-game &lt;agentId&gt; &lt;gameId&gt;
    ///   - leave-game &lt;agentId&gt;
    ///   - list-agents
    ///   - add-prompt &lt;name&gt; &lt;file&gt;
    ///   - edit-prompt &lt;name&gt;
    ///   - list-prompts
    /// </summary>
    class Program
    {
        static async Task<int> Main(string[] args)
        {
            var rootCommand = new RootCommand("Agent CLI - Manage AI agents in the game server");
            
            // Initialize Orleans client (will be used by all commands)
            await using var client = new AgentClient();
            try
            {
                await client.ConnectAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error connecting to Orleans cluster: {ex.Message}");
                Console.WriteLine("Make sure the game server is running.");
                return 1;
            }

            // Agent management commands
            var joinGameCommand = new Command("join-game", "Instruct an agent to join a game");
            var agentIdArg = new Argument<string>("agentId", "The agent ID");
            var gameIdArg = new Argument<string>("gameId", "The game session ID");
            joinGameCommand.AddArgument(agentIdArg);
            joinGameCommand.AddArgument(gameIdArg);
            joinGameCommand.SetHandler(async (string agentId, string gameId) =>
            {
                try
                {
                    var agent = client.GetAgent(agentId);
                    await agent.JoinGameAsync(gameId);
                    Console.WriteLine($"Agent {agentId} joined game {gameId}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error: {ex.Message}");
                }
            }, agentIdArg, gameIdArg);
            rootCommand.AddCommand(joinGameCommand);

            var leaveGameCommand = new Command("leave-game", "Instruct an agent to leave its current game");
            var agentIdArg2 = new Argument<string>("agentId", "The agent ID");
            leaveGameCommand.AddArgument(agentIdArg2);
            leaveGameCommand.SetHandler(async (string agentId) =>
            {
                try
                {
                    var agent = client.GetAgent(agentId);
                    await agent.LeaveGameAsync();
                    Console.WriteLine($"Agent {agentId} left game");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error: {ex.Message}");
                }
            }, agentIdArg2);
            rootCommand.AddCommand(leaveGameCommand);

            var listAgentsCommand = new Command("list-agents", "List all active agents");
            listAgentsCommand.SetHandler(async () =>
            {
                // TODO: Implement proper query for active agents
                // For now, Orleans doesn't provide a built-in way to list all grains
                // This would need a management grain or storage-based tracking
                Console.WriteLine("TODO: List active agents (requires agent registry implementation)");
                await Task.CompletedTask;
            });
            rootCommand.AddCommand(listAgentsCommand);

            // Prompt management commands
            var addPromptCommand = new Command("add-prompt", "Add a new prompt template");
            var promptNameArg = new Argument<string>("name", "Prompt template name");
            var promptFileArg = new Argument<string>("file", "Path to markdown file");
            addPromptCommand.AddArgument(promptNameArg);
            addPromptCommand.AddArgument(promptFileArg);
            addPromptCommand.SetHandler(async (string name, string file) =>
            {
                try
                {
                    if (!File.Exists(file))
                    {
                        Console.WriteLine($"Error: File not found at {file}");
                        return;
                    }
                    var content = await File.ReadAllTextAsync(file);
                    var registry = client.GetPromptRegistry();
                    await registry.AddOrUpdateAsync(name, content);
                    Console.WriteLine($"Added/updated prompt '{name}'");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error adding prompt: {ex.Message}");
                }
            }, promptNameArg, promptFileArg);
            rootCommand.AddCommand(addPromptCommand);

            var listPromptsCommand = new Command("list-prompts", "List all available prompt templates");
            listPromptsCommand.SetHandler(async () =>
            {
                try
                {
                    var registry = client.GetPromptRegistry();
                    var names = await registry.ListAsync();
                    if (names.Count == 0)
                    {
                        Console.WriteLine("No prompts found.");
                        return;
                    }
                    foreach (var n in names)
                        Console.WriteLine(n);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error listing prompts: {ex.Message}");
                }
            });
            rootCommand.AddCommand(listPromptsCommand);

            // Vision control commands (note: these require a game session management grain)
            var visionCommand = new Command("vision", "Control vision modes for game sessions");
            
            var visionDirectionalCommand = new Command("directional", "Enable directional vision mode for a session");
            var sessionIdArg = new Argument<string>("sessionId", "Game session ID");
            visionDirectionalCommand.AddArgument(sessionIdArg);
            visionDirectionalCommand.SetHandler(async (string sessionId) =>
            {
                try
                {
                    var mgmt = client.GetGameManagement();
                    var result = await mgmt.SetDirectionalVisionAsync(sessionId, true);
                    
                    if (result.Success)
                    {
                        Console.WriteLine($"✓ Directional vision enabled for session {sessionId}");
                    }
                    else
                    {
                        Console.WriteLine($"✗ Error: {result.Message}");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"✗ Failed: {ex.Message}");
                }
            }, sessionIdArg);
            visionCommand.AddCommand(visionDirectionalCommand);

            var visionOmniCommand = new Command("omnidirectional", "Disable directional vision (use omnidirectional) for a session");
            var sessionIdArg2 = new Argument<string>("sessionId", "Game session ID");
            visionOmniCommand.AddArgument(sessionIdArg2);
            visionOmniCommand.SetHandler(async (string sessionId) =>
            {
                try
                {
                    var mgmt = client.GetGameManagement();
                    var result = await mgmt.SetDirectionalVisionAsync(sessionId, false);
                    
                    if (result.Success)
                    {
                        Console.WriteLine($"✓ Omnidirectional vision enabled for session {sessionId}");
                    }
                    else
                    {
                        Console.WriteLine($"✗ Error: {result.Message}");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"✗ Failed: {ex.Message}");
                }
            }, sessionIdArg2);
            visionCommand.AddCommand(visionOmniCommand);

            var visionFovCommand = new Command("fov", "Set field of view degrees for a session");
            var fovSessionIdArg = new Argument<string>("sessionId", "Game session ID");
            var degreesArg = new Argument<int>("degrees", "FOV in degrees (1-360)");
            visionFovCommand.AddArgument(fovSessionIdArg);
            visionFovCommand.AddArgument(degreesArg);
            visionFovCommand.SetHandler(async (string sessionId, int degrees) =>
            {
                if (degrees < 1 || degrees > 360)
                {
                    Console.WriteLine("✗ Error: FOV must be between 1 and 360 degrees");
                    return;
                }
                
                try
                {
                    var mgmt = client.GetGameManagement();
                    var result = await mgmt.SetFieldOfViewAsync(sessionId, degrees);
                    
                    if (result.Success)
                    {
                        Console.WriteLine($"✓ FOV set to {degrees}° for session {sessionId}");
                    }
                    else
                    {
                        Console.WriteLine($"✗ Error: {result.Message}");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"✗ Failed: {ex.Message}");
                }
            }, fovSessionIdArg, degreesArg);
            visionCommand.AddCommand(visionFovCommand);

            var visionStatusCommand = new Command("status", "Show vision mode status for a session");
            var sessionIdArg3 = new Argument<string>("sessionId", "Game session ID");
            visionStatusCommand.AddArgument(sessionIdArg3);
            visionStatusCommand.SetHandler(async (string sessionId) =>
            {
                try
                {
                    var mgmt = client.GetGameManagement();
                    var status = await mgmt.GetVisionStatusAsync(sessionId);
                    
                    if (status != null)
                    {
                        Console.WriteLine($"Vision Status for session {sessionId}:");
                        Console.WriteLine($"  Directional Vision: {(status.DirectionalVisionMode ? "ON" : "OFF")}");
                        Console.WriteLine($"  Heading: {status.HeadingDegrees}°");
                        Console.WriteLine($"  Field of View: {status.FieldOfViewDegrees}°");
                        Console.WriteLine($"  Lighting Mode: {status.LightingMode}");
                        Console.WriteLine($"  Vision Mode: {status.VisionMode}");
                    }
                    else
                    {
                        Console.WriteLine($"✗ Session {sessionId} not found");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"✗ Failed: {ex.Message}");
                }
            }, sessionIdArg3);
            visionCommand.AddCommand(visionStatusCommand);

            rootCommand.AddCommand(visionCommand);

            // World management commands
            var worldCommand = new Command("world", "Manage game worlds");
            
            var worldCreateCommand = new Command("create", "Create a new game world");
            var worldNameArg = new Argument<string>("name", "World name");
            var worldDescArg = new Argument<string>("description", "World description");
            var worldGenArg = new Option<string>("--generator", () => "rooms-and-corridors", "Generator type");
            var worldWidthArg = new Option<int>("--width", () => 100, "World width");
            var worldHeightArg = new Option<int>("--height", () => 100, "World height");
            var worldNarrativeArg = new Option<string?>("--narrative", () => null, "Narrative ID");
            worldCreateCommand.AddArgument(worldNameArg);
            worldCreateCommand.AddArgument(worldDescArg);
            worldCreateCommand.AddOption(worldGenArg);
            worldCreateCommand.AddOption(worldWidthArg);
            worldCreateCommand.AddOption(worldHeightArg);
            worldCreateCommand.AddOption(worldNarrativeArg);
            worldCreateCommand.SetHandler(async (string name, string desc, string gen, int width, int height, string? narrativeId) =>
            {
                try
                {
                    var mgmt = client.GetGameManagement();
                    var request = new CreateWorldRequest
                    {
                        Name = name,
                        Description = desc,
                        GeneratorType = gen,
                        GeneratorParameters = new System.Collections.Generic.Dictionary<string, object>
                        {
                            ["Width"] = width,
                            ["Height"] = height
                        },
                        NarrativeId = narrativeId,
                        Size = new WorldSize { Width = width, Height = height, Depth = 1 }
                    };
                    
                    var worldId = await mgmt.CreateWorldAsync(request);
                    Console.WriteLine($"✓ Created world: {worldId}");
                    Console.WriteLine($"  Name: {name}");
                    Console.WriteLine($"  Size: {width}x{height}");
                    Console.WriteLine($"  Generator: {gen}");
                    if (narrativeId != null)
                        Console.WriteLine($"  Narrative: {narrativeId}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"✗ Error creating world: {ex.Message}");
                }
            }, worldNameArg, worldDescArg, worldGenArg, worldWidthArg, worldHeightArg, worldNarrativeArg);
            worldCommand.AddCommand(worldCreateCommand);

            var worldListCommand = new Command("list", "List all game worlds");
            worldListCommand.SetHandler(async () =>
            {
                try
                {
                    var mgmt = client.GetGameManagement();
                    var worlds = await mgmt.ListWorldsAsync();
                    
                    if (worlds.Count == 0)
                    {
                        Console.WriteLine("No worlds found.");
                        return;
                    }
                    
                    Console.WriteLine($"Found {worlds.Count} world(s):\n");
                    foreach (var world in worlds)
                    {
                        Console.WriteLine($"[{world.WorldId}]");
                        Console.WriteLine($"  Name: {world.Name}");
                        Console.WriteLine($"  State: {world.State}");
                        Console.WriteLine($"  Players: {world.PlayerCount}/{world.MaxPlayers}");
                        Console.WriteLine($"  Maps: {world.MapIds.Count}");
                        if (world.NarrativeId != null)
                            Console.WriteLine($"  Narrative: {world.NarrativeId}");
                        Console.WriteLine($"  Created: {world.CreatedAt:yyyy-MM-dd HH:mm:ss}");
                        Console.WriteLine();
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"✗ Error listing worlds: {ex.Message}");
                }
            });
            worldCommand.AddCommand(worldListCommand);

            var worldInfoCommand = new Command("info", "Get detailed world information");
            var worldIdInfoArg = new Argument<string>("worldId", "World ID");
            worldInfoCommand.AddArgument(worldIdInfoArg);
            worldInfoCommand.SetHandler(async (string worldId) =>
            {
                try
                {
                    var mgmt = client.GetGameManagement();
                    var world = await mgmt.GetWorldInfoAsync(worldId);
                    
                    if (world == null)
                    {
                        Console.WriteLine($"✗ World {worldId} not found");
                        return;
                    }
                    
                    Console.WriteLine($"World: {world.Name}");
                    Console.WriteLine($"  ID: {world.WorldId}");
                    Console.WriteLine($"  State: {world.State}");
                    Console.WriteLine($"  Description: {world.Description}");
                    Console.WriteLine($"  Players: {world.PlayerCount}/{world.MaxPlayers}");
                    Console.WriteLine($"  Maps: {string.Join(", ", world.MapIds)}");
                    if (world.NarrativeId != null)
                        Console.WriteLine($"  Narrative: {world.NarrativeId}");
                    Console.WriteLine($"  Created: {world.CreatedAt:yyyy-MM-dd HH:mm:ss}");
                    if (world.LastActivityAt.HasValue)
                        Console.WriteLine($"  Last Activity: {world.LastActivityAt.Value:yyyy-MM-dd HH:mm:ss}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"✗ Error getting world info: {ex.Message}");
                }
            }, worldIdInfoArg);
            worldCommand.AddCommand(worldInfoCommand);

            var worldPauseCommand = new Command("pause", "Pause a running world");
            var worldIdPauseArg = new Argument<string>("worldId", "World ID");
            worldPauseCommand.AddArgument(worldIdPauseArg);
            worldPauseCommand.SetHandler(async (string worldId) =>
            {
                try
                {
                    var mgmt = client.GetGameManagement();
                    var result = await mgmt.PauseWorldAsync(worldId);
                    
                    if (result.Success)
                        Console.WriteLine($"✓ World {worldId} paused");
                    else
                        Console.WriteLine($"✗ Error: {result.Message}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"✗ Error pausing world: {ex.Message}");
                }
            }, worldIdPauseArg);
            worldCommand.AddCommand(worldPauseCommand);

            var worldResumeCommand = new Command("resume", "Resume a paused world");
            var worldIdResumeArg = new Argument<string>("worldId", "World ID");
            worldResumeCommand.AddArgument(worldIdResumeArg);
            worldResumeCommand.SetHandler(async (string worldId) =>
            {
                try
                {
                    var mgmt = client.GetGameManagement();
                    var result = await mgmt.ResumeWorldAsync(worldId);
                    
                    if (result.Success)
                        Console.WriteLine($"✓ World {worldId} resumed");
                    else
                        Console.WriteLine($"✗ Error: {result.Message}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"✗ Error resuming world: {ex.Message}");
                }
            }, worldIdResumeArg);
            worldCommand.AddCommand(worldResumeCommand);

            var worldShutdownCommand = new Command("shutdown", "Shut down and remove a world");
            var worldIdShutdownArg = new Argument<string>("worldId", "World ID");
            worldShutdownCommand.AddArgument(worldIdShutdownArg);
            worldShutdownCommand.SetHandler(async (string worldId) =>
            {
                try
                {
                    var mgmt = client.GetGameManagement();
                    var result = await mgmt.ShutdownWorldAsync(worldId);
                    
                    if (result.Success)
                        Console.WriteLine($"✓ World {worldId} shut down");
                    else
                        Console.WriteLine($"✗ Error: {result.Message}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"✗ Error shutting down world: {ex.Message}");
                }
            }, worldIdShutdownArg);
            worldCommand.AddCommand(worldShutdownCommand);

            rootCommand.AddCommand(worldCommand);

            // Agent runner commands (attach/step/run/stop/status)
            var agentCmd = new Command("agent", "Agent runner orchestration");

            var attachCmd = new Command("attach", "Attach an agent runner to a session");
            var attachSessionArg = new Argument<string>("sessionId", "Session ID to attach to");
            var attachAgentOpt = new Option<string>("--agent", () => "agent-1", "Agent identifier");
            var attachRunnerOpt = new Option<string>("--runner", () => "runner-1", "Runner grain id");
            attachCmd.AddArgument(attachSessionArg);
            attachCmd.AddOption(attachAgentOpt);
            attachCmd.AddOption(attachRunnerOpt);
            attachCmd.SetHandler(async (string sessionId, string agent, string runner) =>
            {
                try
                {
                    var r = client.GetAgentRunner(runner);
                    var ok = await r.AttachAsync(sessionId, agent);
                    Console.WriteLine(ok ? $"✓ Attached {runner} to {sessionId} as {agent}" : $"✗ Failed to attach {runner} to {sessionId}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"✗ Attach failed: {ex.Message}");
                }
            }, attachSessionArg, attachAgentOpt, attachRunnerOpt);
            agentCmd.AddCommand(attachCmd);

            var stepCmd = new Command("step", "Execute one agent step");
            var stepRunnerArg = new Argument<string>("runnerId", "Runner grain id");
            stepCmd.AddArgument(stepRunnerArg);
            stepCmd.SetHandler(async (string runnerId) =>
            {
                try
                {
                    var r = client.GetAgentRunner(runnerId);
                    await r.StepAsync();
                    var s = await r.GetStatusAsync();
                    Console.WriteLine($"✓ Step done. Steps={s.Steps} LastAction={s.LastAction} Result={s.LastResult}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"✗ Step failed: {ex.Message}");
                }
            }, stepRunnerArg);
            agentCmd.AddCommand(stepCmd);

            var runCmd = new Command("run", "Run agent loop");
            var runRunnerArg = new Argument<string>("runnerId", "Runner grain id");
            var runMaxOpt = new Option<int?>("--max-steps", () => 10, "Max steps");
            var runDelayOpt = new Option<int>("--delay", () => 200, "Delay ms between steps");
            runCmd.AddArgument(runRunnerArg);
            runCmd.AddOption(runMaxOpt);
            runCmd.AddOption(runDelayOpt);
            runCmd.SetHandler(async (string runnerId, int? maxSteps, int delay) =>
            {
                try
                {
                    var r = client.GetAgentRunner(runnerId);
                    await r.RunAsync(maxSteps, delay);
                    Console.WriteLine($"✓ Running {runnerId} (maxSteps={maxSteps}, delay={delay}ms)");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"✗ Run failed: {ex.Message}");
                }
            }, runRunnerArg, runMaxOpt, runDelayOpt);
            agentCmd.AddCommand(runCmd);

            var stopCmd = new Command("stop", "Stop agent loop");
            var stopRunnerArg = new Argument<string>("runnerId", "Runner grain id");
            stopCmd.AddArgument(stopRunnerArg);
            stopCmd.SetHandler(async (string runnerId) =>
            {
                try
                {
                    var r = client.GetAgentRunner(runnerId);
                    await r.StopAsync();
                    Console.WriteLine($"✓ Stopped {runnerId}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"✗ Stop failed: {ex.Message}");
                }
            }, stopRunnerArg);
            agentCmd.AddCommand(stopCmd);

            var statusCmd = new Command("status", "Show runner status");
            var statusRunnerArg = new Argument<string>("runnerId", "Runner grain id");
            statusCmd.AddArgument(statusRunnerArg);
            statusCmd.SetHandler(async (string runnerId) =>
            {
                try
                {
                    var r = client.GetAgentRunner(runnerId);
                    var s = await r.GetStatusAsync();
                    Console.WriteLine($"Runner {runnerId}");
                    Console.WriteLine($"  Session: {s.SessionId}");
                    Console.WriteLine($"  Agent:   {s.AgentId}");
                    Console.WriteLine($"  Running: {s.IsRunning}");
                    Console.WriteLine($"  Steps:   {s.Steps}");
                    Console.WriteLine($"  Last:    {s.LastAction} -> {s.LastResult}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"✗ Status failed: {ex.Message}");
                }
            }, statusRunnerArg);
            agentCmd.AddCommand(statusCmd);

            rootCommand.AddCommand(agentCmd);


            // Narrative management commands
            var narrativeCommand = new Command("narrative", "Manage game narratives");
            
            var narrativeCreateCommand = new Command("create", "Create a new narrative");
            var narrativeIdArg = new Argument<string>("narrativeId", "Narrative ID");
            var narrativeNameArg = new Argument<string>("name", "Narrative name");
            var narrativeDescArg = new Argument<string>("description", "Narrative description");
            narrativeCreateCommand.AddArgument(narrativeIdArg);
            narrativeCreateCommand.AddArgument(narrativeNameArg);
            narrativeCreateCommand.AddArgument(narrativeDescArg);
            narrativeCreateCommand.SetHandler(async (string id, string name, string desc) =>
            {
                try
                {
                    var narrative = client.GetNarrative(id);
                    var definition = new NarrativeDefinition
                    {
                        NarrativeId = id,
                        Name = name,
                        Description = desc
                    };
                    
                    await narrative.SetNarrativeAsync(definition);
                    Console.WriteLine($"✓ Created narrative: {id}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"✗ Error creating narrative: {ex.Message}");
                }
            }, narrativeIdArg, narrativeNameArg, narrativeDescArg);
            narrativeCommand.AddCommand(narrativeCreateCommand);

            var narrativeLoadCommand = new Command("load", "Load narrative from JSON file");
            var narrativeFileArg = new Argument<string>("file", "Path to narrative JSON file");
            narrativeLoadCommand.AddArgument(narrativeFileArg);
            narrativeLoadCommand.SetHandler(async (string file) =>
            {
                try
                {
                    if (!File.Exists(file))
                    {
                        Console.WriteLine($"✗ File not found: {file}");
                        return;
                    }
                    
                    var json = await File.ReadAllTextAsync(file);
                    var definition = JsonSerializer.Deserialize<NarrativeDefinition>(json, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });
                    
                    if (definition == null || string.IsNullOrEmpty(definition.NarrativeId))
                    {
                        Console.WriteLine($"✗ Invalid narrative file: missing NarrativeId");
                        return;
                    }
                    
                    var narrative = client.GetNarrative(definition.NarrativeId);
                    await narrative.SetNarrativeAsync(definition);
                    
                    Console.WriteLine($"✓ Loaded narrative: {definition.NarrativeId}");
                    Console.WriteLine($"  Name: {definition.Name}");
                    Console.WriteLine($"  Quests: {definition.Quests.Count}");
                    Console.WriteLine($"  Loot Tables: {definition.LootTables.Count}");
                    Console.WriteLine($"  NPC Goals: {definition.NPCGoals.Count}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"✗ Error loading narrative: {ex.Message}");
                }
            }, narrativeFileArg);
            narrativeCommand.AddCommand(narrativeLoadCommand);

            var narrativeShowCommand = new Command("show", "Show narrative details");
            var narrativeIdShowArg = new Argument<string>("narrativeId", "Narrative ID");
            narrativeShowCommand.AddArgument(narrativeIdShowArg);
            narrativeShowCommand.SetHandler(async (string id) =>
            {
                try
                {
                    var narrative = client.GetNarrative(id);
                    var definition = await narrative.GetNarrativeAsync();
                    
                    if (definition == null)
                    {
                        Console.WriteLine($"✗ Narrative {id} not found");
                        return;
                    }
                    
                    Console.WriteLine($"Narrative: {definition.Name}");
                    Console.WriteLine($"  ID: {definition.NarrativeId}");
                    Console.WriteLine($"  Description: {definition.Description}");
                    Console.WriteLine($"  Quests: {definition.Quests.Count}");
                    
                    if (definition.Quests.Count > 0)
                    {
                        Console.WriteLine($"\n  Quest List:");
                        foreach (var quest in definition.Quests)
                        {
                            Console.WriteLine($"    - {quest.Title} ({quest.QuestId})");
                            Console.WriteLine($"      Objectives: {quest.Objectives.Count}");
                        }
                    }
                    
                    Console.WriteLine($"\n  Loot Tables: {definition.LootTables.Count}");
                    foreach (var kvp in definition.LootTables)
                    {
                        Console.WriteLine($"    - {kvp.Key}: {kvp.Value.Entries.Count} entries");
                    }
                    
                    Console.WriteLine($"\n  Monster Density Rules: {definition.MonsterDensity.Count}");
                    foreach (var kvp in definition.MonsterDensity)
                    {
                        Console.WriteLine($"    - {kvp.Key}: {kvp.Value.MonsterTypes.Count} types");
                    }
                    
                    Console.WriteLine($"\n  NPC Goals: {definition.NPCGoals.Count}");
                    foreach (var goal in definition.NPCGoals)
                    {
                        Console.WriteLine($"    - {goal.GoalId} ({goal.NPCType}): {goal.GoalType}");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"✗ Error showing narrative: {ex.Message}");
                }
            }, narrativeIdShowArg);
            narrativeCommand.AddCommand(narrativeShowCommand);

            var narrativeDeleteCommand = new Command("delete", "Delete a narrative");
            var narrativeIdDeleteArg = new Argument<string>("narrativeId", "Narrative ID");
            narrativeDeleteCommand.AddArgument(narrativeIdDeleteArg);
            narrativeDeleteCommand.SetHandler(async (string id) =>
            {
                try
                {
                    var narrative = client.GetNarrative(id);
                    await narrative.DeleteAsync();
                    Console.WriteLine($"✓ Deleted narrative: {id}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"✗ Error deleting narrative: {ex.Message}");
                }
            }, narrativeIdDeleteArg);
            narrativeCommand.AddCommand(narrativeDeleteCommand);

            rootCommand.AddCommand(narrativeCommand);

            return await rootCommand.InvokeAsync(args);
        }
    }
}

