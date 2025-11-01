using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.CommandLine.Parsing;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Aetherctl.Orleans;
using Aetherium.Server.MultiWorld;

namespace Aetherctl.Commands
{
    public static class ToolsCommands
    {
        public static void AddToRoot(RootCommand root)
        {
            var toolsCmd = new Command("tools", "Tool management and inspection");

            // tools list
            var listCmd = new Command("list", "List all available tools or tools for a specific profile");
            var profileOpt = new Option<string?>("--profile", "Filter tools by profile");
            var categoryOpt = new Option<string?>("--category", "Filter tools by category");
            listCmd.AddOption(profileOpt);
            listCmd.AddOption(categoryOpt);
            listCmd.SetHandler(async (InvocationContext ctx) =>
            {
                try
                {
                    var parseResult = ctx.ParseResult;
                    var profile = parseResult.GetValueForOption(profileOpt);
                    var category = parseResult.GetValueForOption(categoryOpt);
                    await using var factory = new OrleansClientFactory();
                    await factory.ConnectAsync();
                    var mgmt = factory.GetGameManagement();
                    var tools = await mgmt.ListAvailableToolsAsync(profile);

                    if (tools == null || !tools.Any())
                    {
                        if (Common.IsJsonOutput(parseResult))
                            Common.WriteOutput(parseResult, new { success = true, count = 0, tools = Array.Empty<object>() });
                        else
                            Console.WriteLine("No tools found.");
                        return;
                    }

                    var filtered = category != null
                        ? tools.Where(t => t.Categories.Contains(category, StringComparer.OrdinalIgnoreCase))
                        : tools;

                    if (Common.IsJsonOutput(parseResult))
                    {
                        Common.WriteOutput(parseResult, new
                        {
                            success = true,
                            count = filtered.Count(),
                            tools = filtered.Select(t => new
                            {
                                toolId = t.ToolId,
                                description = t.Description,
                                categories = t.Categories,
                                parameters = t.ParameterSchema.Properties.Select(p => new
                                {
                                    name = p.Key,
                                    type = p.Value.Type,
                                    description = p.Value.Description,
                                    required = t.ParameterSchema.Required.Contains(p.Key)
                                })
                            })
                        });
                    }
                    else
                    {
                        Console.WriteLine($"\n{(profile != null ? $"Tools for profile '{profile}'" : "All tools")}:");
                        Console.WriteLine(new string('-', 80));
                        foreach (var tool in filtered.OrderBy(t => t.ToolId))
                        {
                            Console.WriteLine($"\n[{tool.ToolId}]");
                            Console.WriteLine($"  Description: {tool.Description}");
                            Console.WriteLine($"  Categories: {string.Join(", ", tool.Categories)}");
                            if (tool.ParameterSchema.Properties.Any())
                            {
                                Console.WriteLine($"  Parameters:");
                                foreach (var param in tool.ParameterSchema.Properties)
                                {
                                    var required = tool.ParameterSchema.Required.Contains(param.Key) ? "*" : " ";
                                    Console.WriteLine($"    {required} {param.Key}: {param.Value.Type} - {param.Value.Description}");
                                }
                            }
                        }
                        Console.WriteLine();
                    }
                }
                catch (Exception ex)
                {
                    Common.WriteError(ctx.ParseResult, $"Error listing tools: {ex.Message}");
                }
            });

            // tools describe
            var describeCmd = new Command("describe", "Get detailed information about a specific tool");
            var toolIdArg = new Argument<string>("toolId", "Tool ID");
            describeCmd.AddArgument(toolIdArg);
            describeCmd.SetHandler(async (InvocationContext ctx) =>
            {
                try
                {
                    var parseResult = ctx.ParseResult;
                    var toolId = parseResult.GetValueForArgument(toolIdArg);
                    await using var factory = new OrleansClientFactory();
                    await factory.ConnectAsync();
                    var mgmt = factory.GetGameManagement();
                    var tools = await mgmt.ListAvailableToolsAsync(null);
                    var tool = tools?.FirstOrDefault(t => t.ToolId.Equals(toolId, StringComparison.OrdinalIgnoreCase));

                    if (tool == null)
                    {
                        Common.WriteError(parseResult, $"Tool '{toolId}' not found.");
                        return;
                    }

                    if (Common.IsJsonOutput(parseResult))
                    {
                        Common.WriteOutput(parseResult, new
                        {
                            success = true,
                            toolId = tool.ToolId,
                            description = tool.Description,
                            categories = tool.Categories,
                            parameters = tool.ParameterSchema.Properties.Select(p => new
                            {
                                name = p.Key,
                                type = p.Value.Type,
                                description = p.Value.Description,
                                required = tool.ParameterSchema.Required.Contains(p.Key),
                                allowedValues = p.Value.AllowedValues
                            })
                        });
                    }
                    else
                    {
                        Console.WriteLine($"\nTool: {tool.ToolId}");
                        Console.WriteLine(new string('=', 80));
                        Console.WriteLine($"Description: {tool.Description}");
                        Console.WriteLine($"Categories: {string.Join(", ", tool.Categories)}");
                        if (tool.ParameterSchema.Properties.Any())
                        {
                            Console.WriteLine("\nParameters:");
                            foreach (var param in tool.ParameterSchema.Properties)
                            {
                                var required = tool.ParameterSchema.Required.Contains(param.Key);
                                Console.WriteLine($"  • {param.Key} ({param.Value.Type}){(required ? " [REQUIRED]" : "")}");
                                Console.WriteLine($"    {param.Value.Description}");
                                if (param.Value.AllowedValues.Any())
                                {
                                    Console.WriteLine($"    Allowed values: {string.Join(", ", param.Value.AllowedValues)}");
                                }
                            }
                        }
                        Console.WriteLine();
                    }
                }
                catch (Exception ex)
                {
                    Common.WriteError(ctx.ParseResult, $"Error getting tool info: {ex.Message}");
                }
            });

            // tools categories
            var categoriesCmd = new Command("categories", "List all available tool categories");
            categoriesCmd.SetHandler(async (InvocationContext ctx) =>
            {
                try
                {
                    var parseResult = ctx.ParseResult;
                    await using var factory = new OrleansClientFactory();
                    await factory.ConnectAsync();
                    var mgmt = factory.GetGameManagement();
                    var tools = await mgmt.ListAvailableToolsAsync(null);
                    var categories = tools?.SelectMany(t => t.Categories).Distinct().OrderBy(c => c).ToList();

                    if (categories == null || !categories.Any())
                    {
                        if (Common.IsJsonOutput(parseResult))
                            Common.WriteOutput(parseResult, new { success = true, categories = Array.Empty<string>() });
                        else
                            Console.WriteLine("No categories found.");
                        return;
                    }

                    if (Common.IsJsonOutput(parseResult))
                    {
                        Common.WriteOutput(parseResult, new
                        {
                            success = true,
                            categories = categories.Select(c => new
                            {
                                name = c,
                                toolCount = tools.Count(t => t.Categories.Contains(c))
                            })
                        });
                    }
                    else
                    {
                        Console.WriteLine("\nAvailable tool categories:");
                        foreach (var category in categories)
                        {
                            var count = tools.Count(t => t.Categories.Contains(category));
                            Console.WriteLine($"  • {category} ({count} tools)");
                        }
                        Console.WriteLine();
                    }
                }
                catch (Exception ex)
                {
                    Common.WriteError(ctx.ParseResult, $"Error listing categories: {ex.Message}");
                }
            });

            // tools test
            var testCmd = new Command("test", "Execute a tool with provided parameters");
            var testToolIdArg = new Argument<string>("toolId", "Tool ID to execute");
            var sessionIdOpt = new Option<string>("--session-id", "Session ID to execute tool in");
            var argsOpt = new Option<string?>("--args", "Tool arguments as JSON");
            testCmd.AddArgument(testToolIdArg);
            testCmd.AddOption(sessionIdOpt);
            testCmd.AddOption(argsOpt);
            testCmd.SetHandler(async (InvocationContext ctx) =>
            {
                try
                {
                    var parseResult = ctx.ParseResult;
                    var toolId = parseResult.GetValueForArgument(testToolIdArg);
                    var sessionId = parseResult.GetValueForOption(sessionIdOpt);
                    var argsJson = parseResult.GetValueForOption(argsOpt);

                    await using var factory = new OrleansClientFactory();
                    await factory.ConnectAsync();
                    var mgmt = factory.GetGameManagement();

                    var sessionInfo = await mgmt.GetSessionInfoAsync(sessionId);
                    if (sessionInfo == null)
                    {
                        Common.WriteError(parseResult, $"Session '{sessionId}' not found. Use 'session list' to list available sessions.");
                        return;
                    }

                    Dictionary<string, object> args = new();
                    if (!string.IsNullOrEmpty(argsJson))
                    {
                        try
                        {
                            var parsed = JsonSerializer.Deserialize<Dictionary<string, object>>(argsJson, new JsonSerializerOptions
                            {
                                PropertyNameCaseInsensitive = false
                            });
                            if (parsed != null) args = parsed;
                        }
                        catch (JsonException ex)
                        {
                            Common.WriteError(parseResult, $"Invalid JSON format for --args: {ex.Message}");
                            return;
                        }
                    }

                    var tools = await mgmt.ListAvailableToolsAsync(null);
                    var tool = tools?.FirstOrDefault(t => t.ToolId.Equals(toolId, StringComparison.OrdinalIgnoreCase));
                    if (tool == null)
                    {
                        Common.WriteError(parseResult, $"Tool '{toolId}' not found.");
                        return;
                    }

                    if (tool.ParameterSchema.Properties.Any() && args.Count == 0)
                    {
                        if (Common.IsJsonOutput(parseResult))
                        {
                            Common.WriteOutput(parseResult, new
                            {
                                success = false,
                                error = "Tool requires parameters",
                                parameters = tool.ParameterSchema.Properties.Select(p => new
                                {
                                    name = p.Key,
                                    type = p.Value.Type,
                                    description = p.Value.Description,
                                    required = tool.ParameterSchema.Required.Contains(p.Key)
                                })
                            });
                        }
                        else
                        {
                            Console.WriteLine($"\nTool '{toolId}' requires parameters:");
                            foreach (var param in tool.ParameterSchema.Properties)
                            {
                                var required = tool.ParameterSchema.Required.Contains(param.Key);
                                Console.WriteLine($"  {(required ? "*" : " ")} {param.Key} ({param.Value.Type}): {param.Value.Description}");
                            }
                            Console.WriteLine($"\nExample usage:");
                            Console.WriteLine($"  tools test {toolId} --session-id {sessionId} --args {{\"{tool.ParameterSchema.Properties.Keys.First()}\":\"value\"}}");
                        }
                        return;
                    }

                    var result = await mgmt.ExecuteToolAsync(toolId, sessionId, args);

                    if (Common.IsJsonOutput(parseResult))
                    {
                        Common.WriteOutput(parseResult, new
                        {
                            success = result.Success,
                            message = result.Message
                        });
                    }
                    else
                    {
                        if (result.Success)
                        {
                            Console.WriteLine($"✓ Tool '{toolId}' executed successfully");
                            if (!string.IsNullOrEmpty(result.Message))
                                Console.WriteLine($"  Result: {result.Message}");
                        }
                        else
                        {
                            Console.WriteLine($"✗ Tool '{toolId}' execution failed");
                            Console.WriteLine($"  Error: {result.Message}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    Common.WriteError(ctx.ParseResult, $"Error testing tool: {ex.Message}");
                }
            });

            // tools profile (reusing profileCmd from AgentCLI structure)
            var profileCmd = new Command("profile", "Agent tool profile management");
            var profileListCmd = new Command("list", "List all predefined profiles");
            profileListCmd.SetHandler((InvocationContext ctx) =>
            {
                var parseResult = ctx.ParseResult;
                if (Common.IsJsonOutput(parseResult))
                {
                    Common.WriteOutput(parseResult, new
                    {
                        success = true,
                        profiles = new[]
                        {
                            new { name = "explorer", description = "Basic movement and perception" },
                            new { name = "player", description = "Full player capabilities" },
                            new { name = "fullaccess", description = "All player tools" },
                            new { name = "worldbuilder", description = "World editing tools" },
                            new { name = "narrativedesigner", description = "Narrative and quest tools" },
                            new { name = "admin", description = "Unrestricted access" }
                        }
                    });
                }
                else
                {
                    Console.WriteLine("\nPredefined profiles:");
                    Console.WriteLine("  • explorer    - Basic movement and perception");
                    Console.WriteLine("  • player      - Full player capabilities");
                    Console.WriteLine("  • fullaccess  - All player tools");
                    Console.WriteLine("  • worldbuilder - World editing tools");
                    Console.WriteLine("  • narrativedesigner - Narrative and quest tools");
                    Console.WriteLine("  • admin       - Unrestricted access");
                    Console.WriteLine();
                }
            });

            var profileShowCmd = new Command("show", "Show details of a specific profile");
            var profileNameArg = new Argument<string>("profileName", "Profile name");
            profileShowCmd.AddArgument(profileNameArg);
            profileShowCmd.SetHandler(async (InvocationContext ctx) =>
            {
                try
                {
                    var parseResult = ctx.ParseResult;
                    var profileName = parseResult.GetValueForArgument(profileNameArg);
                    await using var factory = new OrleansClientFactory();
                    await factory.ConnectAsync();
                    var mgmt = factory.GetGameManagement();
                    var tools = await mgmt.ListAvailableToolsAsync(profileName);

                    if (tools == null || !tools.Any())
                    {
                        Common.WriteError(parseResult, $"Profile '{profileName}' not found or has no tools.");
                        return;
                    }

                    if (Common.IsJsonOutput(parseResult))
                    {
                        Common.WriteOutput(parseResult, new
                        {
                            success = true,
                            profileName,
                            toolCount = tools.Count(),
                            categories = tools.SelectMany(t => t.Categories).Distinct().OrderBy(c => c).Select(c => new
                            {
                                name = c,
                                toolCount = tools.Count(t => t.Categories.Contains(c))
                            }),
                            tools = tools.OrderBy(t => t.ToolId).Select(t => t.ToolId)
                        });
                    }
                    else
                    {
                        Console.WriteLine($"\nProfile: {profileName}");
                        Console.WriteLine(new string('=', 80));
                        Console.WriteLine($"Tools available: {tools.Count()}");
                        Console.WriteLine("\nCategories:");
                        var categories = tools.SelectMany(t => t.Categories).Distinct().OrderBy(c => c);
                        foreach (var category in categories)
                        {
                            var count = tools.Count(t => t.Categories.Contains(category));
                            Console.WriteLine($"  • {category} ({count} tools)");
                        }
                        Console.WriteLine("\nTools:");
                        foreach (var tool in tools.OrderBy(t => t.ToolId))
                        {
                            Console.WriteLine($"  • {tool.ToolId}");
                        }
                        Console.WriteLine();
                    }
                }
                catch (Exception ex)
                {
                    Common.WriteError(ctx.ParseResult, $"Error showing profile: {ex.Message}");
                }
            });

            profileCmd.AddCommand(profileListCmd);
            profileCmd.AddCommand(profileShowCmd);

            toolsCmd.AddCommand(listCmd);
            toolsCmd.AddCommand(describeCmd);
            toolsCmd.AddCommand(categoriesCmd);
            toolsCmd.AddCommand(testCmd);
            toolsCmd.AddCommand(profileCmd);
            root.AddCommand(toolsCmd);
        }
    }
}

