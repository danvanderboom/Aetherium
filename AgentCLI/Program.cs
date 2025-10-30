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

            return await rootCommand.InvokeAsync(args);
        }
    }
}

