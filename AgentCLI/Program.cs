using System;
using System.CommandLine;
using System.IO;
using System.Threading.Tasks;
using AgentCLI;

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
                        Console.WriteLine($"✗ Error: {result.Reason}");
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
                        Console.WriteLine($"✗ Error: {result.Reason}");
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
                        Console.WriteLine($"✗ Error: {result.Reason}");
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

            return await rootCommand.InvokeAsync(args);
        }
    }
}

